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
    /// </summary>
    /// <remarks>
    /// The effective default is <c>true</c> (not <c>false</c> as the C# property type suggests).
    /// Only participates in the options cascade when explicitly set in the attribute declaration.
    /// If omitted, falls back to assembly-level <see cref="StructUnionOptionsAttribute"/> or the default (true).
    /// </remarks>
    public bool EnableImplicitConversions { get; set; }

    /// <summary>
    /// The name of the generated tag property.
    /// When null, falls back to assembly-level <see cref="StructUnionOptionsAttribute"/> or the default ("Tag").
    /// </summary>
    public string? TagPropertyName { get; set; }

    /// <summary>
    /// When true, generates a nested <c>Cases</c> class with a readonly struct per variant
    /// and <c>As{Variant}</c> accessor properties instead of flat <c>{Variant}{Param}</c> properties.
    /// </summary>
    /// <remarks>
    /// The effective default is <c>false</c>.
    /// Only participates in the options cascade when explicitly set in the attribute declaration.
    /// If omitted, falls back to assembly-level <see cref="StructUnionOptionsAttribute"/> or the default (false).
    /// </remarks>
    public bool NestedAccessors { get; set; }

    /// <summary>
    /// The suffix to trim from the template type name when deriving the generated struct name
    /// (record/class templates only). Overrides the assembly-level <see cref="StructUnionOptionsAttribute.TemplateSuffix"/>.
    /// Cannot be combined with <see cref="GeneratedName"/>.
    /// </summary>
    public string? TemplateSuffix { get; set; }

    /// <summary>
    /// When true, the generated struct implements <see cref="IDisposable"/> (and
    /// <c>IAsyncDisposable</c> when any variant field implements it), and emits per-variant
    /// <c>Take{Variant}</c> / <c>TryTake{Variant}</c> ownership-transfer helpers.
    /// </summary>
    /// <remarks>
    /// The effective default is <c>false</c>.
    /// Only participates in the options cascade when explicitly set in the attribute declaration.
    /// If omitted, falls back to assembly-level <see cref="StructUnionOptionsAttribute"/> or the default (false).
    /// When false and a variant carries a disposable field, the generator reports <c>SU0013</c>.
    /// </remarks>
    public bool GenerateDispose { get; set; }

    /// <summary>
    /// When true, the generated struct is also a C# 15 union type: it carries
    /// <c>[System.Runtime.CompilerServices.Union]</c> and an <c>IUnionMembers</c> provider whose
    /// case types are the generated <c>Cases.{Variant}</c> structs. Native <c>switch</c>
    /// expressions over those case types are then checked for exhaustiveness by the compiler,
    /// and matched without boxing.
    /// </summary>
    /// <remarks>
    /// <para>The effective default is <c>false</c>.
    /// Only participates in the options cascade when explicitly set in the attribute declaration.
    /// If omitted, falls back to assembly-level <see cref="StructUnionOptionsAttribute"/> or the default (false).</para>
    /// <para><b>Enabling this changes how patterns bind to the union.</b> Patterns applied to a
    /// value of the union type unwrap to its contents, so <c>x is Shape</c> becomes a compile
    /// error and <c>x is { Tag: Shape.Tags.Circle }</c> starts matching the contents rather than
    /// the union. That is why it is opt-in.</para>
    /// <para>Requires a target framework that provides
    /// <c>System.Runtime.CompilerServices.UnionAttribute</c> (net11.0 or later, or a polyfill) and
    /// <c>&lt;LangVersion&gt;preview&lt;/LangVersion&gt;</c>; otherwise the generator reports
    /// <c>SU0014</c> or <c>SU0015</c> and omits the union members. Not supported together with
    /// common fields (<c>SU0016</c>).</para>
    /// </remarks>
    public bool NativeUnion { get; set; }

    /// <summary>
    /// When true, the generated struct implements <see cref="IEquatable{T}"/> and gets
    /// <c>Equals</c>, <c>GetHashCode</c>, <c>==</c> and <c>!=</c>. Set to false to suppress all of
    /// them, either because the union is not a value you compare or because you are writing equality
    /// yourself.
    /// </summary>
    /// <remarks>
    /// <para>The effective default is <c>true</c> (not <c>false</c> as the C# property type suggests).
    /// Only participates in the options cascade when explicitly set in the attribute declaration.
    /// If omitted, falls back to assembly-level <see cref="StructUnionOptionsAttribute"/> or the default (true).</para>
    /// <para>Suppressing a <em>single</em> member does not need this flag: declaring any of
    /// <c>Equals</c>, <c>GetHashCode</c> or <c>operator ==</c> on your own partial declaration stops
    /// the generator emitting that one, and the remaining generated members delegate to yours.</para>
    /// </remarks>
    public bool GenerateEquality { get; set; }
}
