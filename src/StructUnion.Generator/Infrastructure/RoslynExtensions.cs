using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StructUnion.Generator.Models;

#pragma warning disable RS1024 // Symbols should be compared for equality

namespace StructUnion.Generator.Infrastructure;

static class RoslynExtensions
{
    public static string GetAccessibilityString(this ISymbol symbol) =>
        symbol.DeclaredAccessibility.ToAccessibilityString();

    public static string ToAccessibilityString(this Accessibility access) =>
        access switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "internal"
        };

    public static string GetNamespaceString(this INamedTypeSymbol symbol) =>
        symbol.ContainingNamespace.IsGlobalNamespace ? "" : symbol.ContainingNamespace.ToDisplayString();

    public static EquatableArray<string> GetContainingTypeChain(this INamedTypeSymbol symbol)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        var parent = symbol.ContainingType;
        while (parent is not null)
        {
            var keyword = parent.IsValueType ? "struct" : parent.IsRecord ? "record class" : "class";
            var typeParams = parent.TypeParameters.Length > 0
                ? $"<{string.Join(", ", parent.TypeParameters.Select(tp => tp.Name))}>"
                : "";
            result.Insert(0, $"partial {keyword} {parent.Name}{typeParams}");
            parent = parent.ContainingType;
        }
        return result.ToImmutable().ToEquatableArray();
    }

    public static EquatableArray<TypeParameterModel> GetTypeParameterModels(this INamedTypeSymbol symbol)
    {
        var result = ImmutableArray.CreateBuilder<TypeParameterModel>();
        foreach (var tp in symbol.TypeParameters)
        {
            var constraints = ImmutableArray.CreateBuilder<string>();
            if (tp.HasReferenceTypeConstraint)
            {
                constraints.Add("class");
            }

            if (tp.HasValueTypeConstraint)
            {
                constraints.Add("struct");
            }

            if (tp.HasUnmanagedTypeConstraint)
            {
                constraints.Add("unmanaged");
            }

            if (tp.HasNotNullConstraint)
            {
                constraints.Add("notnull");
            }

            foreach (var ct in tp.ConstraintTypes)
            {
                constraints.Add(ct.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }

            if (tp.HasConstructorConstraint)
            {
                constraints.Add("new()");
            }

            // Must come last — C# requires `allows ref struct` to be the final constraint clause.
            // Dropping it would silently strip the modifier from the generated partial declaration,
            // leaving a `T` field the containing struct is not allowed to hold.
            if (tp.AllowsRefLikeType)
            {
                constraints.Add("allows ref struct");
            }

            result.Add(new TypeParameterModel(tp.Name, constraints.ToImmutable().ToEquatableArray()));
        }
        return result.ToImmutable().ToEquatableArray();
    }

    public static (bool? EnableImplicit, string? GeneratedName, string? TagPropertyName, bool? NestedAccessors, string? TemplateSuffix, bool? GenerateDispose, bool? NativeUnion, bool? GenerateEquality)
        GetStructUnionAttributeProps(this GeneratorAttributeSyntaxContext ctx)
    {
        bool? enableImplicit = null;
        string? generatedName = null;
        string? tagPropertyName = null;
        bool? nestedAccessors = null;
        string? suffix = null;
        bool? generateDispose = null;
        bool? nativeUnion = null;
        bool? generateEquality = null;

        foreach (var attr in ctx.Attributes)
        {
            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string name)
            {
                generatedName = name;
            }

            foreach (var named in attr.NamedArguments)
            {
                switch (named.Key)
                {
                    case nameof(StructUnionAttribute.EnableImplicitConversions) when named.Value.Value is bool val:
                        enableImplicit = val;
                        break;
                    case nameof(StructUnionAttribute.TagPropertyName) when named.Value.Value is string tagName:
                        tagPropertyName = tagName;
                        break;
                    case nameof(StructUnionAttribute.NestedAccessors) when named.Value.Value is bool nested:
                        nestedAccessors = nested;
                        break;
                    case nameof(StructUnionAttribute.TemplateSuffix) when named.Value.Value is string s:
                        suffix = s;
                        break;
                    case nameof(StructUnionAttribute.GenerateDispose) when named.Value.Value is bool dispose:
                        generateDispose = dispose;
                        break;
                    case nameof(StructUnionAttribute.NativeUnion) when named.Value.Value is bool native:
                        nativeUnion = native;
                        break;
                    case nameof(StructUnionAttribute.GenerateEquality) when named.Value.Value is bool equality:
                        generateEquality = equality;
                        break;
                }
            }
        }

        return (enableImplicit, generatedName, tagPropertyName, nestedAccessors, suffix, generateDispose, nativeUnion, generateEquality);
    }

    /// <summary>
    /// Reads assembly-level options from [assembly: StructUnionOptions].
    /// Returns null for properties not explicitly set by the user.
    /// Extracted into its own IncrementalValueProvider for proper caching —
    /// reading from Compilation in the per-type transform breaks incremental invalidation.
    /// </summary>
    public static AssemblyOptions GetAssemblyOptions(this Compilation compilation)
    {
        string? tagPropertyName = null;
        string? templateSuffix = null;
        bool? enableImplicit = null;
        bool? nestedAccessors = null;
        bool? generateDispose = null;
        bool? nativeUnion = null;
        bool? generateEquality = null;

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == typeof(StructUnionOptionsAttribute).FullName)
            {
                foreach (var named in attr.NamedArguments)
                {
                    switch (named.Key)
                    {
                        case nameof(StructUnionOptionsAttribute.TagPropertyName) when named.Value.Value is string tagName:
                            tagPropertyName = tagName;
                            break;
                        case nameof(StructUnionOptionsAttribute.TemplateSuffix) when named.Value.Value is string suffix:
                            templateSuffix = suffix;
                            break;
                        case nameof(StructUnionOptionsAttribute.EnableImplicitConversions) when named.Value.Value is bool val:
                            enableImplicit = val;
                            break;
                        case nameof(StructUnionOptionsAttribute.NestedAccessors) when named.Value.Value is bool nested:
                            nestedAccessors = nested;
                            break;
                        case nameof(StructUnionOptionsAttribute.GenerateDispose) when named.Value.Value is bool dispose:
                            generateDispose = dispose;
                            break;
                        case nameof(StructUnionOptionsAttribute.NativeUnion) when named.Value.Value is bool native:
                            nativeUnion = native;
                            break;
                        case nameof(StructUnionOptionsAttribute.GenerateEquality) when named.Value.Value is bool equality:
                            generateEquality = equality;
                            break;
                    }
                }
            }
        }

        var languageVersion = compilation is CSharpCompilation csharp
            ? (int)csharp.LanguageVersion
            : 0;

        return new AssemblyOptions(
            tagPropertyName,
            templateSuffix,
            enableImplicit,
            nestedAccessors,
            generateDispose,
            nativeUnion,
            generateEquality,
            HasPublicType(compilation, "System.Runtime.CompilerServices.UnionAttribute"),
            HasPublicType(compilation, "System.Runtime.CompilerServices.IUnion"),
            languageVersion,
            compilation is CSharpCompilation cs ? LanguageVersionFacts.ToDisplayString(cs.LanguageVersion) : "unknown");
    }

    /// <summary>
    /// True if the compilation can see a public type with this metadata name.
    /// </summary>
    /// <remarks>
    /// Uses the plural lookup deliberately. <c>GetTypeByMetadataName</c> returns null when the
    /// name is ambiguous across assemblies, which is exactly what happens when a project both
    /// targets net11.0 and carries a UnionAttribute polyfill — reporting "missing" for a type
    /// that is present twice would be actively wrong.
    /// </remarks>
    static bool HasPublicType(Compilation compilation, string metadataName)
    {
        foreach (var type in compilation.GetTypesByMetadataName(metadataName))
        {
            if (type.DeclaredAccessibility == Accessibility.Public)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the type implements (or is) <see cref="IDisposable"/> and/or
    /// <c>IAsyncDisposable</c>. For type parameters, only constraints are inspected — an
    /// unconstrained <c>T</c> is reported as non-disposable. Matches the design choice to handle
    /// only statically-known disposables (no runtime <c>is IDisposable</c> probing).
    /// </summary>
    public static (bool Sync, bool Async) ClassifyDisposable(this ITypeSymbol type)
    {
        if (type is null)
        {
            return (false, false);
        }

        const string syncFqn = "global::System.IDisposable";
        const string asyncFqn = "global::System.IAsyncDisposable";

        return (IsOrImplements(type, syncFqn), IsOrImplements(type, asyncFqn));

        static bool IsOrImplements(ITypeSymbol type, string interfaceFqn)
        {
            if (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == interfaceFqn)
            {
                return true;
            }

            foreach (var iface in type.AllInterfaces)
            {
                if (iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == interfaceFqn)
                {
                    return true;
                }
            }

            if (type is ITypeParameterSymbol tp)
            {
                foreach (var c in tp.ConstraintTypes)
                {
                    if (IsOrImplements(c, interfaceFqn))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Finds which equality members the user already declared on the union, so the generator can
    /// skip exactly those instead of colliding with them.
    /// </summary>
    /// <remarks>
    /// Generated members are not visible here — a source generator sees only the pre-generation
    /// compilation — so there are no false positives from the generator's own output. Explicit
    /// interface implementations are deliberately not detected: they are named
    /// <c>System.IEquatable&lt;T&gt;.Equals</c> rather than <c>Equals</c>, and they occupy a separate
    /// declaration space, so they cannot collide with a generated member anyway.
    /// </remarks>
    public static UserEqualityMembers GetUserEqualityMembers(this INamedTypeSymbol symbol)
    {
        var equalsSelf = false;
        var equalsObject = false;

        foreach (var member in symbol.GetMembers("Equals"))
        {
            if (member is not IMethodSymbol { Parameters.Length: 1 } method)
            {
                continue;
            }

            var parameterType = method.Parameters[0].Type;
            if (SymbolEqualityComparer.Default.Equals(parameterType, symbol))
            {
                equalsSelf = true;
            }
            else if (parameterType.SpecialType == SpecialType.System_Object)
            {
                equalsObject = true;
            }
        }

        var getHashCode = false;
        foreach (var member in symbol.GetMembers("GetHashCode"))
        {
            if (member is IMethodSymbol { Parameters.Length: 0 })
            {
                getHashCode = true;
            }
        }

        // == and != are treated as one unit: C# requires them in pairs, so generating the other half
        // of a user's operator would silently pair two operators with different semantics.
        var operators =
            HasUserDefinedOperator(symbol, WellKnownMemberNames.EqualityOperatorName)
            || HasUserDefinedOperator(symbol, WellKnownMemberNames.InequalityOperatorName);

        return new(equalsSelf, equalsObject, getHashCode, operators);

        static bool HasUserDefinedOperator(INamedTypeSymbol symbol, string name)
        {
            foreach (var member in symbol.GetMembers(name))
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator })
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// True if the type overrides <see cref="object.ToString"/> with its own implementation.
    /// </summary>
    /// <remarks>
    /// Only meaningful for ref-like types, and load-bearing for them: calling the <em>inherited</em>
    /// <c>object.ToString()</c> on a ref struct receiver needs a boxing conversion, which is illegal,
    /// so <c>field.ToString()</c> only compiles when the type declares its own override.
    /// <c>Span&lt;T&gt;</c> and <c>ReadOnlySpan&lt;T&gt;</c> do; a plain user <c>ref struct</c> usually
    /// does not. A type parameter never counts — there is no way to know what it will be.
    /// </remarks>
    public static bool OverridesToString(this ITypeSymbol type)
    {
        if (type is null or ITypeParameterSymbol)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType)
            {
                break;
            }

            foreach (var member in current.GetMembers(nameof(ToString)))
            {
                if (member is IMethodSymbol { Parameters.Length: 0, IsOverride: true })
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines how a ref-like type can be compared: whether it declares an <c>operator ==</c>
    /// and whether it implements <c>IEquatable&lt;itself&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>Only meaningful for ref-like types. Every other type has
    /// <c>EqualityComparer&lt;T&gt;.Default</c> as a universal fallback, but a ref-like type cannot
    /// be a generic type argument there, and cannot be boxed to reach <c>object.Equals</c> — so if
    /// it declares neither of these, it cannot be compared at all.</para>
    /// <para><c>Span&lt;T&gt;</c> and <c>ReadOnlySpan&lt;T&gt;</c> declare <c>operator ==</c> and do
    /// <em>not</em> implement <c>IEquatable</c>, which is why both probes are needed rather than
    /// just the interface one.</para>
    /// <para>As with <see cref="ClassifyDisposable"/>, a type parameter is judged only by its
    /// constraints — an unconstrained <c>T</c> reports false for both.</para>
    /// </remarks>
    public static (bool DeclaresEqualityOperator, bool ImplementsIEquatableSelf) ClassifyEquality(
        this ITypeSymbol type)
    {
        if (type is null)
        {
            return (false, false);
        }

        return (DeclaresEqualityOperator(type), ImplementsIEquatableSelf(type));

        static bool DeclaresEqualityOperator(ITypeSymbol type)
        {
            foreach (var member in type.GetMembers(WellKnownMemberNames.EqualityOperatorName))
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator })
                {
                    return true;
                }
            }

            if (type is ITypeParameterSymbol tp)
            {
                foreach (var c in tp.ConstraintTypes)
                {
                    if (DeclaresEqualityOperator(c))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool ImplementsIEquatableSelf(ITypeSymbol type)
        {
            foreach (var iface in type.AllInterfaces)
            {
                // The type argument varies, so this cannot compare a fully-qualified string the way
                // ClassifyDisposable does — the open generic and the argument are matched separately.
                if (iface is { IsGenericType: true, TypeArguments.Length: 1 }
                    && iface.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        == "global::System.IEquatable<T>"
                    && SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], type))
                {
                    return true;
                }
            }

            if (type is ITypeParameterSymbol tp)
            {
                foreach (var c in tp.ConstraintTypes)
                {
                    if (ImplementsIEquatableSelf(c))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
