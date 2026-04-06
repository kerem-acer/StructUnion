namespace StructUnion;

/// <summary>
/// Assembly-level attribute to configure default options for StructUnion source generation.
/// Per-type <see cref="StructUnionAttribute"/> values take precedence over these defaults.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
#if STRUCT_UNION_GENERATOR
internal
#else
public
#endif
sealed class StructUnionOptionsAttribute : Attribute
{
    /// <summary>
    /// The default name of the generated tag property.
    /// Can be overridden per-type via <see cref="StructUnionAttribute.TagPropertyName"/>.
    /// </summary>
    public string? TagPropertyName { get; set; }

    /// <summary>
    /// The suffix to trim from record/class template names when deriving the generated struct name.
    /// Can be overridden per-type via <see cref="StructUnionAttribute.TemplateSuffix"/>.
    /// </summary>
    public string? TemplateSuffix { get; set; }

    /// <summary>
    /// When true, generates implicit conversion operators for single-parameter variants
    /// with unique parameter types.
    /// Can be overridden per-type via <see cref="StructUnionAttribute.EnableImplicitConversions"/>.
    /// </summary>
    public bool EnableImplicitConversions { get; set; }

    /// <summary>
    /// When true, generates a nested Cases class with a readonly struct per variant
    /// and As{Variant} accessor properties instead of flat {Variant}{Param} properties.
    /// Can be overridden per-type via <see cref="StructUnionAttribute.NestedAccessors"/>.
    /// </summary>
    public bool NestedAccessors { get; set; }
}
