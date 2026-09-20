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
    bool? GenerateDispose,
    bool? NativeUnion,
    bool? GenerateEquality,
    // Compilation capabilities, projected here rather than through a second CompilationProvider
    // node. All primitives, so the record's structural equality keeps the cache key stable.
    bool HasUnionAttributeType,
    bool HasIUnionType,
    int LanguageVersion,
    string LanguageVersionDisplay)
{
    /// <summary>C# 15, the version that introduced union types.</summary>
    public const int UnionsLanguageVersion = 1500;

    /// <summary>True if the effective language version enables union behaviours.</summary>
    public bool LanguageSupportsUnions => LanguageVersion >= UnionsLanguageVersion;
}
