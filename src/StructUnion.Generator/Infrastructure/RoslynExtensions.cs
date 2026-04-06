using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
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

    public static (bool EnableImplicit, string? ExplicitName) GetStructUnionAttributeProps(
        this GeneratorAttributeSyntaxContext ctx)
    {
        var enableImplicit = true;
        string? explicitName = null;

        foreach (var attr in ctx.Attributes)
        {
            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string name)
            {
                explicitName = name;
            }

            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "EnableImplicitConversions" && named.Value.Value is bool val)
                {
                    enableImplicit = val;
                }
            }
        }

        return (enableImplicit, explicitName);
    }

    /// <summary>
    /// Reads the RecordSuffix from [assembly: StructUnionSettings] if present.
    /// </summary>
    public static string GetRecordSuffix(this Compilation compilation)
    {
        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == "StructUnion.StructUnionSettingsAttribute")
            {
                foreach (var named in attr.NamedArguments)
                {
                    if (named.Key == "RecordSuffix" && named.Value.Value is string suffix)
                    {
                        return suffix;
                    }
                }
            }
        }

        return Parsing.NamingConventions.DefaultRecordSuffix;
    }
}
