using StructUnion.Generator.Infrastructure;

namespace StructUnion.Generator.Models;

enum GenerationMode
{
    /// <summary>Augment user's readonly partial struct with static partial methods.</summary>
    PartialStruct,

    /// <summary>Generate entire struct from record/class template.</summary>
    RecordTemplate
}

readonly record struct UnionModel(
    string Namespace,
    EquatableArray<string> ContainingTypes,
    string Accessibility,
    string Name,
    EquatableArray<TypeParameterModel> TypeParameters,
    EquatableArray<VariantModel> Variants,
    EquatableArray<FieldModel> CommonFields,
    LayoutStrategy Layout,
    bool EnableImplicitConversions,
    int RefZoneOffset,
    int ValueZoneOffset,
    int TotalSize,
    int StructAlignment,
    GenerationMode Mode,
    string TagPropertyName,
    bool NestedAccessors,
    string TemplateTypeName = "",
    string TemplateTypeKeyword = "")
{
    public bool HasCommonFields => CommonFields.Count > 0;

    public string TagField => "_tag";

    public string VariantField(string variantName, string paramName) =>
        $"_{variantName.ToLowerInvariant()}_{paramName}";

    public string FullHintName
    {
        get
        {
            var prefix = Namespace.Length > 0 ? $"{Namespace}." : "";
            foreach (var ct in ContainingTypes)
            {
                var name = ct.Substring(ct.LastIndexOf(' ') + 1);
                prefix += $"{name}.";
            }

            return $"{prefix}{Name}";
        }
    }

    public string TypeNameWithParameters
    {
        get
        {
            if (TypeParameters.Count == 0)
            {
                return Name;
            }

            return $"{Name}<{string.Join(", ", TypeParameters.Select(tp => tp.Name))}>";
        }
    }

    public string FullyQualifiedTypeName
    {
        get
        {
            var prefix = Namespace.Length > 0 ? $"global::{Namespace}." : "global::";
            foreach (var ct in ContainingTypes)
            {
                prefix += $"{ct}.";
            }

            return $"{prefix}{TypeNameWithParameters}";
        }
    }
}
