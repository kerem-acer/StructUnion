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
        NestedAccessors: false);

    public static ParseResult Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
        {
            return new ParseResult(null, EquatableArray<DiagnosticInfo>.Empty);
        }

        return ctx.TargetNode switch
        {
            StructDeclarationSyntax structSyntax => ParseStruct(ctx, symbol, structSyntax, ct),
            RecordDeclarationSyntax => ParseTemplate(ctx, symbol, ct),
            ClassDeclarationSyntax => ParseTemplate(ctx, symbol, ct),
            _ => new ParseResult(null, EquatableArray<DiagnosticInfo>.Empty)
        };
    }

    // ── Struct API ──

    static ParseResult ParseStruct(
        GeneratorAttributeSyntaxContext ctx, INamedTypeSymbol symbol,
        StructDeclarationSyntax syntax, CancellationToken ct)
    {
        var (perTypeImplicit, _, perTypeTag, perTypeNested, _) = ctx.GetStructUnionAttributeProps();
        var (asmTag, _, asmImplicit, asmNested) = ctx.SemanticModel.Compilation.GetAssemblyOptions();

        var enableImplicit = perTypeImplicit ?? asmImplicit ?? Defaults.EnableImplicitConversions;
        var tagPropertyName = perTypeTag ?? asmTag ?? Defaults.TagPropertyName;
        var nestedAccessors = perTypeNested ?? asmNested ?? Defaults.NestedAccessors;
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var location = syntax.Identifier.GetLocation();

        if (!syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.StructMustBePartial, location, symbol.Name));
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (!syntax.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.StructMustBeReadonly, location, symbol.Name));
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        // Variants: static partial methods returning Self
        var variants = ImmutableArray.CreateBuilder<VariantModel>();
        byte tag = FirstVariantTag;
        foreach (var member in symbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is not IMethodSymbol { IsStatic: true, IsPartialDefinition: true } method)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, symbol))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.MethodMustReturnContainingType, method.Locations[0],
                    method.Name, symbol.Name));
                continue;
            }

            var parameters = ExtractMethodParameters(method, diagnostics);
            if (parameters == null)
            {
                continue;
            }

            variants.Add(new VariantModel(method.Name, parameters.Value.ToEquatableArray(), tag++));
        }

        if (variants.Count == 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.NoVariantsFound, location, symbol.Name));
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (variants.Count > MaxVariants)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.TooManyVariants, location,
                symbol.Name, variants.Count.ToString()));
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (HasCaseInsensitiveDuplicate(variants, location, diagnostics))
        {
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (HasReservedVariantName(variants, symbol.Name, location, diagnostics))
        {
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        var noCommonFields = ImmutableArray<FieldModel>.Empty;
        var variantsArr = variants.ToImmutable();

        if (HasTagPropertyNameConflict(variantsArr, noCommonFields, tagPropertyName, symbol.Name, location, diagnostics))
        {
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        var model = BuildModel(symbol, variantsArr, noCommonFields, enableImplicit,
            symbol.Name, GenerationMode.PartialStruct, tagPropertyName, nestedAccessors);

        CheckLargeStruct(model, location, diagnostics);

        return new ParseResult(model, diagnostics.ToImmutable().ToEquatableArray());
    }

    // ── Template API (record or class) ──

    static ParseResult ParseTemplate(
        GeneratorAttributeSyntaxContext ctx, INamedTypeSymbol symbol, CancellationToken ct)
    {
        var (perTypeImplicit, generatedName, perTypeTag, perTypeNested, perTypeSuffix) = ctx.GetStructUnionAttributeProps();
        var (asmTag, asmSuffix, asmImplicit, asmNested) = ctx.SemanticModel.Compilation.GetAssemblyOptions();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var location = ctx.TargetNode.GetLocation();

        if (generatedName is not null && perTypeSuffix is not null)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.GeneratedNameAndSuffixConflict, location, symbol.Name));
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        var enableImplicit = perTypeImplicit ?? asmImplicit ?? Defaults.EnableImplicitConversions;
        var tagPropertyName = perTypeTag ?? asmTag ?? Defaults.TagPropertyName;
        var nestedAccessors = perTypeNested ?? asmNested ?? Defaults.NestedAccessors;
        var effectiveSuffix = perTypeSuffix ?? asmSuffix ?? Defaults.TemplateSuffix;

        var structName = NamingConventions.DeriveStructName(symbol.Name, generatedName, effectiveSuffix);

        // Common fields from primary constructor params + declared properties
        var commonFields = ExtractTemplateCommonFields(symbol);

        // Variants: nested types (records or classes)
        var variants = ImmutableArray.CreateBuilder<VariantModel>();
        byte tag = FirstVariantTag;
        foreach (var nested in symbol.GetTypeMembers())
        {
            ct.ThrowIfCancellationRequested();
            variants.Add(new VariantModel(
                nested.Name, ExtractNestedTypeParameters(nested).ToEquatableArray(), tag++));
        }

        if (variants.Count == 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.NoVariantsFound, location, symbol.Name));
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (variants.Count > MaxVariants)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.TooManyVariants, location,
                symbol.Name, variants.Count.ToString()));
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (HasCaseInsensitiveDuplicate(variants, location, diagnostics))
        {
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (HasReservedVariantName(variants, symbol.Name, location, diagnostics))
        {
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        if (HasTagPropertyNameConflict(variants.ToImmutable(), commonFields.ToImmutable(), tagPropertyName, symbol.Name, location, diagnostics))
        {
            return new ParseResult(null, diagnostics.ToImmutable().ToEquatableArray());
        }

        var templateKeyword = symbol.IsRecord ? "record" : "class";
        var model = BuildModel(symbol, variants.ToImmutable(), commonFields.ToImmutable(), enableImplicit,
            structName, GenerationMode.RecordTemplate, tagPropertyName, nestedAccessors, symbol.Name, templateKeyword);

        CheckLargeStruct(model, location, diagnostics);

        return new ParseResult(model, diagnostics.ToImmutable().ToEquatableArray());
    }

    // ── Shared model builder ──

    static UnionModel BuildModel(
        INamedTypeSymbol symbol,
        ImmutableArray<VariantModel> variants,
        ImmutableArray<FieldModel> commonFields,
        bool enableImplicit,
        string name,
        GenerationMode mode,
        string tagPropertyName,
        bool nestedAccessors,
        string templateTypeName = "",
        string templateTypeKeyword = "")
    {
        var layout = LayoutCalculator.DetermineStrategy(variants, commonFields);
        var (refZoneOffset, valueZoneOffset) = layout == LayoutStrategy.Explicit
            ? LayoutCalculator.ComputeZoneOffsets(commonFields, variants)
            : (0, 0);
        var (totalSize, structAlignment) = layout == LayoutStrategy.Explicit
            ? LayoutCalculator.ComputeTotalSize(variants, commonFields, refZoneOffset, valueZoneOffset)
            : (0, 0);

        return new UnionModel(
            symbol.GetNamespaceString(),
            symbol.GetContainingTypeChain(),
            symbol.GetAccessibilityString(),
            name,
            symbol.GetTypeParameterModels(),
            variants.ToEquatableArray(),
            commonFields.ToEquatableArray(),
            layout, enableImplicit, refZoneOffset, valueZoneOffset,
            totalSize, structAlignment, mode,
            tagPropertyName, nestedAccessors, templateTypeName, templateTypeKeyword);
    }

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
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.DuplicateVariantNameCaseInsensitive, location,
                    variant.Name, existing));
                return true;
            }

            seen[variant.Name] = variant.Name;
        }

        return false;
    }

    static readonly HashSet<string> ReservedVariantNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Default",
        "Tags"
    };

    static bool HasReservedVariantName(
        ImmutableArray<VariantModel>.Builder variants,
        string typeName,
        Location location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var variant in variants)
        {
            if (ReservedVariantNames.Contains(variant.Name))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.ReservedVariantName, location,
                    variant.Name, typeName));
                return true;
            }
        }

        return false;
    }

    static bool HasTagPropertyNameConflict(
        ImmutableArray<VariantModel> variants,
        ImmutableArray<FieldModel> commonFields,
        string tagPropertyName,
        string typeName,
        Location location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        // Check if any variant name matches the tag property name
        foreach (var variant in variants)
        {
            if (string.Equals(variant.Name, tagPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.TagPropertyNameConflict, location,
                    tagPropertyName, typeName));
                return true;
            }
        }

        // Check if any common field name matches the tag property name
        foreach (var field in commonFields)
        {
            if (string.Equals(field.Name, tagPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.TagPropertyNameConflict, location,
                    tagPropertyName, typeName));
                return true;
            }
        }

        return false;
    }

    static void CheckLargeStruct(
        UnionModel model, Location location, ImmutableArray<DiagnosticInfo>.Builder diagnostics)
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
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.LargeStructWarning, location,
                model.Name, totalPayload.ToString()));
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
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, GetMethod: not null } prop
                && !prop.IsImplicitlyDeclared
                && seen.Add(prop.Name))
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
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, GetMethod: not null } prop
                && !prop.IsImplicitlyDeclared
                && seen.Add(prop.Name))
            {
                result.Add(CreateFieldModel(prop.Name, prop.Type, prop.DeclaredAccessibility));
            }
        }

        return result.ToImmutable();
    }

    static ImmutableArray<FieldModel>? ExtractMethodParameters(
        IMethodSymbol method, ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var result = ImmutableArray.CreateBuilder<FieldModel>();
        foreach (var param in method.Parameters)
        {
            if (param.RefKind != RefKind.None)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.RefParametersNotSupported, method.Locations[0],
                    method.Name, param.Name));
                return null;
            }
            result.Add(CreateFieldModel(param.Name, param.Type, Accessibility.Public));
        }
        return result.ToImmutable();
    }

    static FieldModel CreateFieldModel(string name, ITypeSymbol type, Accessibility access) =>
        new(name, type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            access.ToAccessibilityString(), type.IsUnmanagedType,
            TypeClassifier.GetSize(type), TypeClassifier.GetAlignment(type));
}
