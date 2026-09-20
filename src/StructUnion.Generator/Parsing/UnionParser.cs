using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Parsing;

static class UnionParser
{
    const int MaxVariants = 255;
    const byte FirstVariantTag = 1;
    const int LargeStructThreshold = 64;

    /// <summary>
    /// Default option values — lowest priority in the cascade:
    /// StructUnionAttribute ?? StructUnionOptions (user) ?? Defaults
    /// </summary>
    static readonly StructUnionOptions Defaults = new(
        TagPropertyName: "Tag",
        TemplateSuffix: "Record",
        EnableImplicitConversions: true,
        NestedAccessors: false,
        NativeUnion: false,
        GenerateEquality: true);

    // ── Phase 1: Transform (per-type, cached by incremental generator) ──

    /// <summary>
    /// Extracts per-type data from Roslyn symbols. Does NOT read assembly options
    /// (that would break incremental caching). Assembly-option-dependent validation
    /// is deferred to <see cref="ResolveAndBuild"/>.
    /// </summary>
    public static TransformResult ExtractTypeData(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
        {
            return new TransformResult(null, EquatableArray<DiagnosticInfo>.Empty);
        }

        return ctx.TargetNode switch
        {
            StructDeclarationSyntax structSyntax => ExtractStruct(
                ctx,
                symbol,
                structSyntax,
                ct),
            RecordDeclarationSyntax => ExtractTemplate(ctx, symbol, ct),
            ClassDeclarationSyntax => ExtractTemplate(ctx, symbol, ct),
            _ => new TransformResult(null, EquatableArray<DiagnosticInfo>.Empty)
        };
    }

    // ── Phase 2: Resolve + Build (after Combine with assembly options) ──

    /// <summary>
    /// Resolves the options cascade (per-type → assembly → defaults), performs
    /// option-dependent validation, and builds the final <see cref="UnionModel"/>.
    /// </summary>
    public static ParseResult ResolveAndBuild(TypeExtract data, AssemblyOptions asm)
    {
        var enableImplicit = data.PerTypeImplicit ?? asm.EnableImplicit ?? Defaults.EnableImplicitConversions;
        var tagPropertyName = data.PerTypeTag ?? asm.TagPropertyName ?? Defaults.TagPropertyName;
        var nestedAccessors = data.PerTypeNested ?? asm.NestedAccessors ?? Defaults.NestedAccessors;
        var effectiveSuffix = data.PerTypeSuffix ?? asm.TemplateSuffix ?? Defaults.TemplateSuffix;
        var generateDispose = data.PerTypeGenerateDispose ?? asm.GenerateDispose ?? false;
        var requestedNativeUnion = data.PerTypeNativeUnion ?? asm.NativeUnion ?? Defaults.NativeUnion;
        var generateEquality = data.PerTypeGenerateEquality ?? asm.GenerateEquality ?? Defaults.GenerateEquality;

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var location = data.Location;

        // Must run before the reserved-name checks: whether 'Cases' and 'IUnionMembers' are
        // reserved depends on the *effective* native-union flag, not the requested one.
        var nativeUnion = ResolveNativeUnion(data, asm, requestedNativeUnion, location, diagnostics);
        var emitCases = nestedAccessors || nativeUnion;

        // Derive struct name (for template mode)
        var structName = data.Mode == GenerationMode.RecordTemplate ? NamingConventions.DeriveStructName(data.SymbolName, data.GeneratedName, effectiveSuffix) : data.SymbolName;

        // Validate GeneratedName and TagPropertyName as valid C# identifiers
        if (data.GeneratedName is not null && !CSharpIdentifiers.IsValidIdentifier(data.GeneratedName))
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.InvalidIdentifier,
                    location,
                    data.GeneratedName,
                    "GeneratedName",
                    data.SymbolName));

            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (data.PerTypeTag is not null && !CSharpIdentifiers.IsValidIdentifier(data.PerTypeTag))
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.InvalidIdentifier,
                    location,
                    data.PerTypeTag,
                    "TagPropertyName",
                    data.SymbolName));

            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (HasReservedVariantName(
            data.Variants,
            data.SymbolName,
            emitCases,
            nativeUnion,
            location,
            diagnostics))
        {
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (HasReservedCommonFieldName(
            data.CommonFields,
            data.SymbolName,
            emitCases,
            nativeUnion,
            location,
            diagnostics))
        {
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (HasTagPropertyNameConflict(
            data.Variants,
            data.CommonFields,
            tagPropertyName,
            data.SymbolName,
            location,
            diagnostics))
        {
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        var model = BuildModel(
            data,
            structName,
            enableImplicit,
            tagPropertyName,
            nestedAccessors,
            generateDispose,
            nativeUnion,
            nativeUnion && asm.HasIUnionType,
            generateEquality);

        // Must abort: without `ref` on the declaration, every ref-like field is a CS8345 inside
        // generated code, and the rest of the emitted members would pile more errors on top.
        if (HasRefLikeFieldWithoutRefDeclaration(model, data.IsRefLikeDeclaration, location, diagnostics))
        {
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        CheckLargeStruct(model, location, diagnostics);
        ReportDisposableWithoutOptIn(model, location, diagnostics);
        ReportRefLikeFieldNotComparable(model, location, diagnostics);

        return new ParseResult(model, diagnostics.ToImmutable().ToEquatableArray());
    }

    // ── Struct API extraction ──

    static TransformResult ExtractStruct(
        GeneratorAttributeSyntaxContext ctx,
        INamedTypeSymbol symbol,
        StructDeclarationSyntax syntax,
        CancellationToken ct)
    {
        var (perTypeImplicit, generatedName, perTypeTag, perTypeNested, perTypeSuffix, perTypeGenerateDispose, perTypeNativeUnion, perTypeGenerateEquality) = ctx.GetStructUnionAttributeProps();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var location = syntax.Identifier.GetLocation();

        if (!syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.StructMustBePartial,
                    location,
                    symbol.Name));

            return new TransformResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (!syntax.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.StructMustBeReadonly,
                    location,
                    symbol.Name));

            return new TransformResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        // Variants: static partial methods returning Self
        var variants = ImmutableArray.CreateBuilder<VariantModel>();
        var tagValue = (int)FirstVariantTag;
        foreach (var member in symbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is not IMethodSymbol { IsStatic: true, IsPartialDefinition: true } method)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, symbol))
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.MethodMustReturnContainingType,
                        method.Locations[0],
                        method.Name,
                        symbol.Name));

                continue;
            }

            var parameters = ExtractMethodParameters(method, diagnostics);
            if (parameters == null)
            {
                continue;
            }

            variants.Add(new VariantModel(method.Name, parameters.Value.ToEquatableArray(), (byte)tagValue));
            tagValue++;
        }

        if (variants.Count == 0)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.NoVariantsFound,
                    location,
                    symbol.Name));

            return new TransformResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (variants.Count > MaxVariants)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.TooManyVariants,
                    location,
                    symbol.Name,
                    variants.Count.ToString()));

            return new TransformResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (HasCaseInsensitiveDuplicate(variants, location, diagnostics))
        {
            return new TransformResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (generatedName is not null && perTypeSuffix is not null)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.GeneratedNameAndSuffixConflict,
                    location,
                    symbol.Name));

            return new TransformResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        var extract = new TypeExtract(
            symbol.GetNamespaceString(),
            symbol.GetContainingTypeChain(),
            symbol.GetAccessibilityString(),
            symbol.Name,
            symbol.GetTypeParameterModels(),
            variants.ToImmutable().ToEquatableArray(),
            ImmutableArray<FieldModel>.Empty.ToEquatableArray(),
            GenerationMode.PartialStruct,
            "",
            perTypeImplicit,
            generatedName,
            perTypeTag,
            perTypeNested,
            perTypeSuffix,
            perTypeGenerateDispose,
            perTypeNativeUnion,
            perTypeGenerateEquality,
            symbol.GetUserEqualityMembers(),
            symbol.IsRefLikeType,
            DiagnosticLocation.From(location));

        return new TransformResult(extract, diagnostics.ToImmutable().ToEquatableArray());
    }

    // ── Template API extraction ──

    static TransformResult ExtractTemplate(
        GeneratorAttributeSyntaxContext ctx,
        INamedTypeSymbol symbol,
        CancellationToken ct)
    {
        var (perTypeImplicit, generatedName, perTypeTag, perTypeNested, perTypeSuffix, perTypeGenerateDispose, perTypeNativeUnion, perTypeGenerateEquality) = ctx.GetStructUnionAttributeProps();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var location = ctx.TargetNode.GetLocation();

        if (generatedName is not null && perTypeSuffix is not null)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.GeneratedNameAndSuffixConflict,
                    location,
                    symbol.Name));

            return new TransformResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        // Common fields from primary constructor params + declared properties
        var commonFields = ExtractTemplateCommonFields(symbol);

        // Variants: nested types (records or classes)
        var variants = ImmutableArray.CreateBuilder<VariantModel>();
        var tagValue = (int)FirstVariantTag;
        foreach (var nested in symbol.GetTypeMembers())
        {
            ct.ThrowIfCancellationRequested();
            variants.Add(
                new VariantModel(
                    nested.Name,
                    ExtractNestedTypeParameters(nested).ToEquatableArray(),
                    (byte)tagValue));

            tagValue++;
        }

        if (variants.Count == 0)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.NoVariantsFound,
                    location,
                    symbol.Name));

            return new TransformResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (variants.Count > MaxVariants)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.TooManyVariants,
                    location,
                    symbol.Name,
                    variants.Count.ToString()));

            return new TransformResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (HasCaseInsensitiveDuplicate(variants, location, diagnostics))
        {
            return new TransformResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        var templateKeyword = symbol.IsRecord ? "record" : "class";

        var extract = new TypeExtract(
            symbol.GetNamespaceString(),
            symbol.GetContainingTypeChain(),
            symbol.GetAccessibilityString(),
            symbol.Name,
            symbol.GetTypeParameterModels(),
            variants.ToImmutable().ToEquatableArray(),
            commonFields.ToImmutable().ToEquatableArray(),
            GenerationMode.RecordTemplate,
            templateKeyword,
            perTypeImplicit,
            generatedName,
            perTypeTag,
            perTypeNested,
            perTypeSuffix,
            perTypeGenerateDispose,
            perTypeNativeUnion,
            perTypeGenerateEquality,
            // The template's own members belong to the template type, not to the generated struct,
            // so nothing the user wrote there can collide with a generated equality member.
            UserEquality: default,
            // A record or class cannot hold a ref-like member, so a template can never need `ref`.
            IsRefLikeDeclaration: false,
            DiagnosticLocation.From(location));

        return new TransformResult(extract, diagnostics.ToImmutable().ToEquatableArray());
    }

    // ── Model builder ──

    static UnionModel BuildModel(
        TypeExtract data,
        string structName,
        bool enableImplicit,
        string tagPropertyName,
        bool nestedAccessors,
        bool generateDispose,
        bool nativeUnion,
        bool implementIUnion,
        bool generateEquality)
    {
        var variants = data.Variants.AsImmutableArray();
        var commonFields = data.CommonFields.AsImmutableArray();

        var layout = LayoutCalculator.DetermineStrategy(variants, commonFields);
        var (refZoneOffset, valueZoneOffset) = layout == LayoutStrategy.Explicit ? LayoutCalculator.ComputeZoneOffsets(commonFields, variants) : (0, 0);
        var (totalSize, structAlignment) = layout == LayoutStrategy.Explicit ?
            LayoutCalculator.ComputeTotalSize(
                variants,
                commonFields,
                refZoneOffset,
                valueZoneOffset) :
            (0, 0);

        return new UnionModel(
            data.Namespace,
            data.ContainingTypes,
            data.Accessibility,
            structName,
            data.TypeParameters,
            data.Variants,
            data.CommonFields,
            layout,
            enableImplicit,
            refZoneOffset,
            valueZoneOffset,
            totalSize,
            structAlignment,
            data.Mode,
            tagPropertyName,
            nestedAccessors,
            generateDispose,
            nativeUnion,
            implementIUnion,
            data.Mode == GenerationMode.RecordTemplate ? data.SymbolName : "",
            data.TemplateTypeKeyword,
            generateEquality,
            data.UserEquality);
    }

    static void ReportDisposableWithoutOptIn(
        UnionModel model,
        DiagnosticLocation location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (model.GenerateDispose || !model.HasAnyDisposable)
        {
            return;
        }

        foreach (var variant in model.Variants)
        {
            foreach (var param in variant.Parameters)
            {
                if (param.IsDisposable || param.IsAsyncDisposable)
                {
                    diagnostics.Add(
                        DiagnosticInfo.Create(
                            DiagnosticDescriptors.DisposableFieldWithoutOptIn,
                            location,
                            variant.Name,
                            param.Name,
                            param.TypeFullyQualified,
                            model.Name));

                    return; // one per type is enough
                }
            }
        }
    }

    /// <summary>
    /// Reports SU0018 when a union carries a ref-like field but its declaration is not <c>ref</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately an error rather than silently emitting <c>ref</c> on the generator's own partial
    /// declaration. That does work — the compiler merges the modifier across partials — but it would
    /// silently make the user's type non-boxable, unusable as a field of a class and unusable in a
    /// <c>List&lt;T&gt;</c>. Generation must abort: without the modifier every ref-like field is a
    /// CS8345 inside generated code.
    /// </remarks>
    static bool HasRefLikeFieldWithoutRefDeclaration(
        UnionModel model,
        bool isRefLikeDeclaration,
        DiagnosticLocation location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (isRefLikeDeclaration || !model.HasAnyRefLikeField)
        {
            return false;
        }

        foreach (var variant in model.Variants)
        {
            foreach (var param in variant.Parameters)
            {
                if (param.IsRefLike)
                {
                    diagnostics.Add(
                        DiagnosticInfo.Create(
                            DiagnosticDescriptors.RefStructFieldRequiresRefDeclaration,
                            location,
                            variant.Name,
                            param.Name,
                            param.TypeFullyQualified,
                            model.Name));

                    return true;
                }
            }
        }

        // A ref-like common field with no ref-like variant parameter — only reachable in template
        // mode, where a record cannot hold one, so this is defensive rather than expected.
        foreach (var field in model.CommonFields)
        {
            if (field.IsRefLike)
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.RefStructFieldRequiresRefDeclaration,
                        location,
                        model.Name,
                        field.Name,
                        field.TypeFullyQualified,
                        model.Name));

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reports SU0019 when a ref-like field cannot be compared, so equality was skipped.
    /// </summary>
    /// <remarks>
    /// Silent when the user wrote their own <c>Equals(Self)</c> — that is a valid remedy, so warning
    /// about the field would be noise. Also silent when equality was turned off outright.
    /// </remarks>
    static void ReportRefLikeFieldNotComparable(
        UnionModel model,
        DiagnosticLocation location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (!model.GenerateEquality
            || model.UserEquality.DeclaresEqualsSelf
            || model.AllFieldsComparable)
        {
            return;
        }

        foreach (var variant in model.Variants)
        {
            foreach (var param in variant.Parameters)
            {
                if (!param.IsComparable)
                {
                    diagnostics.Add(
                        DiagnosticInfo.Create(
                            DiagnosticDescriptors.RefStructFieldNotComparable,
                            location,
                            variant.Name,
                            param.Name,
                            param.TypeFullyQualified,
                            model.Name));

                    return; // one per type is enough
                }
            }
        }

        foreach (var field in model.CommonFields)
        {
            if (!field.IsComparable)
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.RefStructFieldNotComparable,
                        location,
                        model.Name,
                        field.Name,
                        field.TypeFullyQualified,
                        model.Name));

                return;
            }
        }
    }

    // ── Validation helpers ──

    static bool HasCaseInsensitiveDuplicate(
        ImmutableArray<VariantModel>.Builder variants,
        Location location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variant in variants)
        {
            if (seen.TryGetValue(variant.Name, out var existing))
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.DuplicateVariantNameCaseInsensitive,
                        location,
                        variant.Name,
                        existing));

                return true;
            }

            seen[variant.Name] = variant.Name;
        }

        return false;
    }

    /// <summary>
    /// Resolves whether native union members can actually be emitted, reporting SU0014/15/16.
    /// </summary>
    /// <remarks>
    /// The two availability rules are warnings rather than errors on purpose. A library that
    /// multi-targets declares <c>NativeUnion = true</c> once and compiles it for every target
    /// framework, most of which have no UnionAttribute; an error would make that impossible.
    /// Common fields are a hard error instead, because there is no honest way to round-trip them
    /// through a single-parameter Create.
    /// </remarks>
    static bool ResolveNativeUnion(
        TypeExtract data,
        AssemblyOptions asm,
        bool requested,
        DiagnosticLocation location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (!requested)
        {
            return false;
        }

        if (!asm.HasUnionAttributeType)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.NativeUnionNotSupportedByTarget,
                    location,
                    data.SymbolName));

            return false;
        }

        if (!asm.LanguageSupportsUnions)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.NativeUnionRequiresLanguageVersion,
                    location,
                    data.SymbolName,
                    asm.LanguageVersionDisplay));

            return false;
        }

        if (data.CommonFields.Count > 0)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.NativeUnionWithCommonFields,
                    location,
                    data.SymbolName,
                    data.CommonFields[0].Name));

            return false;
        }

        // Runs before BuildModel, so there is no UnionModel to ask — walk the extract directly.
        // The union contract surfaces cases through a boxing `object? Value`, which a ref struct can
        // never satisfy. Like the common-fields rule this is an error that still degrades rather than
        // aborting: the user keeps a working union, just without the native members.
        foreach (var variant in data.Variants)
        {
            foreach (var param in variant.Parameters)
            {
                if (param.IsRefLike)
                {
                    diagnostics.Add(
                        DiagnosticInfo.Create(
                            DiagnosticDescriptors.NativeUnionWithRefStructField,
                            location,
                            data.SymbolName,
                            variant.Name,
                            param.Name,
                            param.TypeFullyQualified));

                    return false;
                }
            }
        }

        return true;
    }

    static bool HasReservedCommonFieldName(
        EquatableArray<FieldModel> commonFields,
        string typeName,
        bool emitCases,
        bool nativeUnion,
        DiagnosticLocation location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var field in commonFields)
        {
            if (IsGeneratedNestedTypeName(field.Name, emitCases, nativeUnion))
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.ReservedMemberName,
                        location,
                        field.Name,
                        typeName));

                return true;
            }
        }

        return false;
    }

    // The comparer is load-bearing: variant names are matched case-insensitively.
    static readonly HashSet<string> ReservedVariantNames = new(
        ["Default", "Tags"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True if the name collides with a nested type the generator is about to emit. Nested types
    /// and members share one declaration space, so such a collision is a CS0102 in generated code.
    /// </summary>
    static bool IsGeneratedNestedTypeName(string name, bool emitCases, bool nativeUnion) =>
        (emitCases && string.Equals(name, "Cases", StringComparison.OrdinalIgnoreCase))
        || (nativeUnion && string.Equals(name, "IUnionMembers", StringComparison.OrdinalIgnoreCase));

    static bool HasReservedVariantName(
        EquatableArray<VariantModel> variants,
        string typeName,
        bool emitCases,
        bool nativeUnion,
        DiagnosticLocation location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var variant in variants)
        {
            if (ReservedVariantNames.Contains(variant.Name) || IsGeneratedNestedTypeName(variant.Name, emitCases, nativeUnion))
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.ReservedVariantName,
                        location,
                        variant.Name,
                        typeName));

                return true;
            }
        }

        return false;
    }

    static bool HasTagPropertyNameConflict(
        EquatableArray<VariantModel> variants,
        EquatableArray<FieldModel> commonFields,
        string tagPropertyName,
        string typeName,
        DiagnosticLocation location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        // Check if any variant name matches the tag property name
        foreach (var variant in variants)
        {
            if (string.Equals(variant.Name, tagPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.TagPropertyNameConflict,
                        location,
                        tagPropertyName,
                        typeName));

                return true;
            }
        }

        // Check if any common field name matches the tag property name
        foreach (var field in commonFields)
        {
            if (string.Equals(field.Name, tagPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.TagPropertyNameConflict,
                        location,
                        tagPropertyName,
                        typeName));

                return true;
            }
        }

        return false;
    }

    static void CheckLargeStruct(
        UnionModel model,
        DiagnosticLocation location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var commonSize = 0;
        foreach (var field in model.CommonFields)
        {
            if (field.Size < 0)
            {
                return; // unknowable, skip check
            }

            commonSize += field.Size;
        }

        var maxPayload = 0;
        foreach (var variant in model.Variants)
        {
            var payload = 0;
            foreach (var param in variant.Parameters)
            {
                if (param.Size < 0)
                {
                    return; // unknowable, skip check
                }

                payload += param.Size;
            }

            maxPayload = Math.Max(maxPayload, payload);
        }

        var totalPayload = commonSize + maxPayload;
        if (totalPayload > LargeStructThreshold)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    DiagnosticDescriptors.LargeStructWarning,
                    location,
                    model.Name,
                    totalPayload.ToString()));
        }
    }

    // ── Field extraction ──

    static ImmutableArray<FieldModel>.Builder ExtractTemplateCommonFields(INamedTypeSymbol symbol)
    {
        var result = ImmutableArray.CreateBuilder<FieldModel>();
        var seen = new HashSet<string>();

        // Record primary constructor parameters
        var primaryCtor = symbol.Constructors
            .FirstOrDefault(c => !c.IsImplicitlyDeclared && c.Parameters.Length > 0);

        if (primaryCtor is not null)
        {
            foreach (var param in primaryCtor.Parameters)
            {
                if (seen.Add(param.Name))
                {
                    result.Add(CreateFieldModel(param.Name, param.Type, Accessibility.Public));
                }
            }
        }

        // Explicitly declared properties
        foreach (var member in symbol.GetMembers())
        {
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, GetMethod: not null } prop && !prop.IsImplicitlyDeclared && seen.Add(prop.Name))
            {
                result.Add(CreateFieldModel(prop.Name, prop.Type, prop.DeclaredAccessibility));
            }
        }

        return result;
    }

    static ImmutableArray<FieldModel> ExtractNestedTypeParameters(INamedTypeSymbol nested)
    {
        var result = ImmutableArray.CreateBuilder<FieldModel>();
        var seen = new HashSet<string>();

        var primaryCtor = nested.Constructors
            .FirstOrDefault(c => !c.IsImplicitlyDeclared && c.Parameters.Length > 0);

        if (primaryCtor is not null)
        {
            foreach (var param in primaryCtor.Parameters)
            {
                if (seen.Add(param.Name))
                {
                    result.Add(CreateFieldModel(param.Name, param.Type, Accessibility.Public));
                }
            }
        }

        foreach (var member in nested.GetMembers())
        {
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, GetMethod: not null } prop && !prop.IsImplicitlyDeclared && seen.Add(prop.Name))
            {
                result.Add(CreateFieldModel(prop.Name, prop.Type, prop.DeclaredAccessibility));
            }
        }

        return result.ToImmutable();
    }

    static ImmutableArray<FieldModel>? ExtractMethodParameters(
        IMethodSymbol method,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var result = ImmutableArray.CreateBuilder<FieldModel>();
        foreach (var param in method.Parameters)
        {
            if (param.RefKind != RefKind.None)
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        DiagnosticDescriptors.RefParametersNotSupported,
                        method.Locations[0],
                        method.Name,
                        param.Name));

                return null;
            }

            result.Add(CreateFieldModel(param.Name, param.Type, Accessibility.Public));
        }

        return result.ToImmutable();
    }

    static FieldModel CreateFieldModel(string name, ITypeSymbol type, Accessibility access)
    {
        var (fqn, size, alignment) = TypeClassifier.Classify(type);
        var (sync, asyncDisp) = type.ClassifyDisposable();

        // A type parameter declared `allows ref struct` is ref-like for every purpose that matters
        // here — it forces the union to be a ref struct and it cannot be a generic type argument.
        var isRefLike = type.IsRefLikeType || type is ITypeParameterSymbol { AllowsRefLikeType: true };

        // Only ref-like types need these; every other type has EqualityComparer<T>.Default to fall
        // back on and can go straight into an interpolation hole.
        var (eqOperator, equatableSelf) = isRefLike ? type.ClassifyEquality() : (false, false);
        var overridesToString = isRefLike && type.OverridesToString();

        return new(
            name,
            fqn,
            access.ToAccessibilityString(),
            type.IsValueType,
            type.IsUnmanagedType,
            size,
            alignment,
            sync,
            asyncDisp,
            isRefLike,
            eqOperator,
            equatableSelf,
            overridesToString);
    }
}
