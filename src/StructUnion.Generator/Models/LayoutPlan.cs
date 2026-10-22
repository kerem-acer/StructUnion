namespace StructUnion.Generator.Models;

/// <summary>
/// The layout chosen for a union and the sizes that follow from it.
/// </summary>
/// <param name="Strategy">How the fields are laid out.</param>
/// <param name="RefZoneOffset">Start of the overlapping reference fields in the explicit-layout struct.</param>
/// <param name="ValueZoneOffset">Start of the overlapping value fields in the explicit-layout struct.</param>
/// <param name="TotalSize">
/// Size of the explicit-layout struct: the union itself under <see cref="LayoutStrategy.Explicit"/>,
/// its nested storage under <see cref="LayoutStrategy.Hybrid"/>, 0 under <see cref="LayoutStrategy.Auto"/>.
/// </param>
/// <param name="StructAlignment">Alignment of that explicit-layout struct.</param>
/// <param name="EstimatedSize">
/// The union's size as the runtime will lay it out: exact under <see cref="LayoutStrategy.Explicit"/>
/// and for a sequential union without references, an estimate otherwise, -1 if a field's size is unknown.
/// </param>
/// <param name="EstimatedAlignment">The union's alignment, -1 if a field's alignment is unknown.</param>
/// <param name="HoldsReferences">True if any field is, or contains, a reference.</param>
readonly record struct LayoutPlan(
    LayoutStrategy Strategy,
    int RefZoneOffset,
    int ValueZoneOffset,
    int TotalSize,
    int StructAlignment,
    int EstimatedSize,
    int EstimatedAlignment,
    bool HoldsReferences);
