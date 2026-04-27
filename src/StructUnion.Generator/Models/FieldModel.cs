namespace StructUnion.Generator.Models;

/// <summary>
/// Represents a typed member: either a common field (shared across variants)
/// or a variant parameter.
/// </summary>
readonly record struct FieldModel(
    string Name,
    string TypeFullyQualified,
    string Accessibility,
    bool IsValueType,
    bool IsUnmanaged,
    int Size,
    int Alignment,
    bool IsDisposable = false,
    bool IsAsyncDisposable = false);
