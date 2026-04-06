namespace StructUnion;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StructUnionAttribute : Attribute
{
    /// <summary>
    /// Optional explicit name for the generated struct.
    /// If not set, the generator trims a "Record" suffix from the declaring type name.
    /// </summary>
    public string? Name { get; }

    public StructUnionAttribute() { }

    public StructUnionAttribute(string name) => Name = name;

    /// <summary>
    /// When true, generates implicit conversion operators for single-parameter variants
    /// with unique parameter types. Default is true.
    /// </summary>
    public bool EnableImplicitConversions { get; set; } = true;
}
