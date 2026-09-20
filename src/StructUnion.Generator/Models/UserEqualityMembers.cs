namespace StructUnion.Generator.Models;

/// <summary>
/// Which equality members the user already declared on their own partial declaration.
/// </summary>
/// <remarks>
/// <para>Members the user wrote are not generated, per member. Without this a hand-written
/// <c>Equals(Self)</c> collides as CS0111 — and Roslyn anchors that error to whichever declaration
/// it sees second, normally the <em>generated</em> file, so the error points at code the user never
/// wrote.</para>
/// <para><see cref="DeclaresEqualityOperators"/> covers <c>==</c> and <c>!=</c> as a single unit: C#
/// requires them in pairs, so emitting a generated <c>!=</c> beside a user-written <c>==</c> would
/// silently pair two operators with different semantics.</para>
/// <para>Always <c>default</c> in template mode, where the user's members belong to the template
/// type rather than to the generated struct.</para>
/// <para>The <c>Declares</c> prefix is not decoration: a positional parameter named
/// <c>GetHashCode</c> collides with <see cref="object.GetHashCode"/> on the generated record member.</para>
/// </remarks>
readonly record struct UserEqualityMembers(
    bool DeclaresEqualsSelf,
    bool DeclaresEqualsObject,
    bool DeclaresGetHashCode,
    bool DeclaresEqualityOperators);
