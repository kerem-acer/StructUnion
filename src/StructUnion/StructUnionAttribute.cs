namespace StructUnion;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false)]
#if STRUCT_UNION_GENERATOR
internal
#else
public
#endif
sealed class StructUnionAttribute : Attribute
{
    /// <summary>
    /// Optional explicit name for the generated struct (record/class templates only).
    /// If not set, the generator trims the configured suffix from the declaring type name.
    /// Cannot be combined with <see cref="TemplateSuffix"/>.
    /// </summary>
    public string? GeneratedName { get; }

    public StructUnionAttribute() { }

    public StructUnionAttribute(string generatedName) => GeneratedName = generatedName;

    /// <summary>
    /// When true, generates implicit conversion operators for single-parameter variants
    /// with unique parameter types.
    /// When not set, falls back to assembly-level <see cref="StructUnionOptionsAttribute"/> or the default (true).
    /// </summary>
    public bool EnableImplicitConversions { get; set; }

    /// <summary>
    /// The name of the generated tag property.
    /// When null, falls back to assembly-level <see cref="StructUnionOptionsAttribute"/> or the default ("Tag").
    /// </summary>
    public string? TagPropertyName { get; set; }

    /// <summary>
    /// When true, generates a nested <c>Cases</c> class with a readonly struct per variant
    /// and <c>As{Variant}</c> accessor properties instead of flat <c>{Variant}{Param}</c> properties.
    /// When not set, falls back to assembly-level <see cref="StructUnionOptionsAttribute"/> or the default (false).
    /// </summary>
    public bool NestedAccessors { get; set; }

    /// <summary>
    /// The suffix to trim from the template type name when deriving the generated struct name
    /// (record/class templates only). Overrides the assembly-level <see cref="StructUnionOptionsAttribute.TemplateSuffix"/>.
    /// Cannot be combined with <see cref="GeneratedName"/>.
    /// </summary>
    public string? TemplateSuffix { get; set; }
}
