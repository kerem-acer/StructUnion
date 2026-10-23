namespace StructUnion.Generator.Models;

/// <summary>
/// Represents a typed member: either a common field (shared across variants)
/// or a variant parameter.
/// </summary>
readonly record struct FieldModel(
    string Name,
    string TypeFullyQualified,
    string Accessibility,
    bool IsValueType,
    bool IsUnmanaged,
    int Size,
    int Alignment,
    bool IsDisposable = false,
    bool IsAsyncDisposable = false,
    bool IsRefLike = false,
    bool DeclaresEqualityOperator = false,
    bool ImplementsIEquatableSelf = false,
    bool OverridesToString = false,
    string? UnannotatedTypeFullyQualified = null)
{
    /// <summary>
    /// The type with its nullable reference annotations removed: <c>string</c> for a
    /// <c>string?</c> field, which is one type to the compiler. Generated members that overload on
    /// the field's type must differ in this, not in <see cref="TypeFullyQualified"/>, which keeps
    /// the annotations so the code it is written into says what the user's declaration says.
    /// </summary>
    /// <remarks>Falls back to <see cref="TypeFullyQualified"/> for a model built without it.</remarks>
    public string TypeIdentity => UnannotatedTypeFullyQualified ?? TypeFullyQualified;

    /// <summary>
    /// True if a ref-like field can be compared at all. A ref-like type cannot be a generic type
    /// argument, so <c>EqualityComparer&lt;T&gt;.Default</c> — the fallback for every other type —
    /// is unavailable, leaving only an <c>operator ==</c> or a strongly-typed <c>Equals</c>.
    /// </summary>
    public bool IsComparable => !IsRefLike || DeclaresEqualityOperator || ImplementsIEquatableSelf;

    /// <summary>
    /// True if the field's value can appear in an interpolated string.
    /// </summary>
    /// <remarks>
    /// A ref-like field cannot go in a bare interpolation hole — that routes through the handler's
    /// generic <c>AppendFormatted&lt;T&gt;</c> — and it can only be given an explicit
    /// <c>ToString()</c> call if its type overrides <c>ToString</c>, since reaching the inherited
    /// <c>object.ToString</c> would box it.
    /// </remarks>
    public bool IsRenderable => !IsRefLike || OverridesToString;
}
