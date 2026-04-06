namespace StructUnion.Generator.Models;

/// <summary>
/// Resolved options after cascade: per-type attribute → assembly options → static defaults.
/// </summary>
readonly record struct StructUnionOptions(
    string TagPropertyName,
    string TemplateSuffix,
    bool EnableImplicitConversions,
    bool NestedAccessors);
