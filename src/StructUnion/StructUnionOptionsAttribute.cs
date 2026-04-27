namespace StructUnion;

/// <summary>
/// Assembly-level attribute to configure default options for StructUnion source generation.
/// Per-type <see cref="StructUnionAttribute"/> values take precedence over these defaults.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
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
    /// </summary>
    /// <remarks>
    /// Only participates in the options cascade when explicitly set in the attribute declaration.
    /// Can be overridden per-type via <see cref="StructUnionAttribute.EnableImplicitConversions"/>.
    /// </remarks>
    public bool EnableImplicitConversions { get; set; }

    /// <summary>
    /// When true, generates a nested Cases class with a readonly struct per variant
    /// and As{Variant} accessor properties instead of flat {Variant}{Param} properties.
    /// </summary>
    /// <remarks>
    /// Only participates in the options cascade when explicitly set in the attribute declaration.
    /// Can be overridden per-type via <see cref="StructUnionAttribute.NestedAccessors"/>.
    /// </remarks>
    public bool NestedAccessors { get; set; }

    /// <summary>
    /// When true, the generated struct implements <see cref="IDisposable"/>
    /// (and <c>IAsyncDisposable</c> when applicable) and emits per-variant
    /// <c>Take{Variant}</c> / <c>TryTake{Variant}</c> ownership-transfer helpers.
    /// </summary>
    /// <remarks>
    /// Only participates in the options cascade when explicitly set in the attribute declaration.
    /// Can be overridden per-type via <see cref="StructUnionAttribute.GenerateDispose"/>.
    /// </remarks>
    public bool GenerateDispose { get; set; }
}
