namespace StructUnion.Generator.Models;

enum LayoutStrategy
{
    /// <summary>All fields have known size/alignment — LayoutKind.Explicit with overlapping FieldOffset.</summary>
    Explicit,

    /// <summary>Generic unions and ref-like fields — no overlapping, sequential layout.</summary>
    Auto,

    /// <summary>
    /// Some fields cannot overlap (value types holding references, types of unknown size). The tag and
    /// every field that can overlap live in a nested explicit-layout struct; the runtime lays out the rest.
    /// </summary>
    Hybrid
}
