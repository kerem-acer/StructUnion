namespace StructUnion.Generator.Models;

/// <summary>
/// Assembly-level options extracted from [assembly: StructUnionOptions].
/// Separated into its own IncrementalValueProvider for proper caching.
/// </summary>
readonly record struct AssemblyOptions(
    string? TagPropertyName,
    string? TemplateSuffix,
    bool? EnableImplicit,
    bool? NestedAccessors,
    bool? GenerateDispose);
