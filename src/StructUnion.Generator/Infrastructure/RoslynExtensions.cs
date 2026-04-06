using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using StructUnion;
using StructUnion.Generator.Models;

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
            result.Insert(0, $"partial {keyword} {parent.Name}");
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

            result.Add(new TypeParameterModel(tp.Name, constraints.ToImmutable().ToEquatableArray()));
        }
        return result.ToImmutable().ToEquatableArray();
    }

    public static (bool? EnableImplicit, string? GeneratedName, string? TagPropertyName, bool? NestedAccessors, string? TemplateSuffix)
        GetStructUnionAttributeProps(this GeneratorAttributeSyntaxContext ctx)
    {
        bool? enableImplicit = null;
        string? generatedName = null;
        string? tagPropertyName = null;
        bool? nestedAccessors = null;
        string? suffix = null;

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
                }
            }
        }

        return (enableImplicit, generatedName, tagPropertyName, nestedAccessors, suffix);
    }

    /// <summary>
    /// Reads assembly-level options from [assembly: StructUnionOptions].
    /// Returns null for properties not explicitly set by the user.
    /// </summary>
    public static (string? TagPropertyName, string? TemplateSuffix, bool? EnableImplicit, bool? NestedAccessors)
        GetAssemblyOptions(this Compilation compilation)
    {
        string? tagPropertyName = null;
        string? templateSuffix = null;
        bool? enableImplicit = null;
        bool? nestedAccessors = null;

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == typeof(StructUnionOptionsAttribute).FullName!)
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
                    }
                }
            }
        }

        return (tagPropertyName, templateSuffix, enableImplicit, nestedAccessors);
    }
}
