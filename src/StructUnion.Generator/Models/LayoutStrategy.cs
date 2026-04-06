namespace StructUnion.Generator.Models;

enum LayoutStrategy
{
    /// <summary>All fields have known size/alignment — LayoutKind.Explicit with overlapping FieldOffset.</summary>
    Explicit,

    /// <summary>Unconstrained generics or unknown sizes — no overlapping, LayoutKind.Auto.</summary>
    Auto
}
