namespace StructUnion;

/// <summary>
/// Assembly-level attribute to configure StructUnion source generation settings.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class StructUnionSettingsAttribute : Attribute
{
    /// <summary>
    /// The suffix to trim from record/class template names when deriving the generated struct name.
    /// Default is "Record".
    /// </summary>
    public string RecordSuffix { get; set; } = "Record";
}
