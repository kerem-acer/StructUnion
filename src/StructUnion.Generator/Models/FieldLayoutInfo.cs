namespace StructUnion.Generator.Models;

/// <summary>
/// Computed field offset for a single field in the explicit layout.
/// </summary>
readonly record struct FieldLayoutInfo(
    string FieldName,
    string TypeFullyQualified,
    int Offset,
    string? VariantName);
