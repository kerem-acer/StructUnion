using StructUnion.Generator.Infrastructure;

namespace StructUnion.Generator.Models;

readonly record struct VariantModel(
    string Name,
    EquatableArray<FieldModel> Parameters,
    byte Tag)
{
    /// <summary>Pre-computed lowercase variant name for field name generation.</summary>
    public string NameLower { get; } = Name.ToLowerInvariant();

    /// <summary>Returns the backing field name for a variant parameter.</summary>
    public string FieldName(string paramName) => $"_{NameLower}_{paramName}";
}
