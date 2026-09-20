namespace StructUnion.IntegrationTests.RefStructApi;

[StructUnion]
public readonly ref partial struct Payload
{
    public static partial Payload Bytes(Span<byte> data);
    public static partial Payload Text(ReadOnlySpan<char> text);
    public static partial Payload Number(int value);
    public static partial Payload Empty();
}

/// <summary>
/// Exercises a ref-struct union at runtime.
/// </summary>
/// <remarks>
/// Three constraints shape every test here, all of them consequences of the union being a ref struct:
/// <list type="bullet">
/// <item>TUnit's <c>Assert.That&lt;T&gt;</c> is generic, so the union value itself can never be passed
/// to it — assertions run on projections or on an already-reduced <c>bool</c>.</item>
/// <item>A ref-like local cannot live in an <c>async</c> method (CS4013), so the span work happens in
/// a <c>static</c> local function that returns primitives.</item>
/// <item><c>Unsafe.SizeOf&lt;T&gt;</c> has no <c>allows ref struct</c>, so there is no size assertion
/// here of the kind <c>RefTypeTests</c> and <c>SmallUnionTests</c> make.</item>
/// </list>
/// </remarks>
public class RefStructTests
{
    [Test]
    public async Task Bytes_RoundTripsThroughStackalloc()
    {
        await Assert.That(Probe()).IsEqualTo(3);

        static int Probe()
        {
            Span<byte> data = stackalloc byte[3];
            data[0] = 1;
            var p = Payload.Bytes(data);
            return p.BytesData.Length;
        }
    }

    [Test]
    public async Task Bytes_PreservesContents()
    {
        await Assert.That(Probe()).IsEqualTo((byte)7);

        static byte Probe()
        {
            Span<byte> data = stackalloc byte[3];
            data[1] = 7;
            return Payload.Bytes(data).BytesData[1];
        }
    }

    [Test]
    public async Task Bytes_WritesThroughToTheOriginalBuffer()
    {
        // The union stores the span, not a copy of its contents — mutating through the accessor is
        // visible in the caller's array.
        var buffer = new byte[] { 0, 0, 0 };
        Payload.Bytes(buffer).BytesData[2] = 9;

        await Assert.That(buffer[2]).IsEqualTo((byte)9);
    }

    [Test]
    public async Task Text_RoundTripsReadOnlySpan()
    {
        await Assert.That(Probe()).IsEqualTo("hello");

        static string Probe() => Payload.Text("hello".AsSpan()).TextText.ToString();
    }

    [Test]
    public async Task IsChecks_DiscriminateVariants()
    {
        var (bytesIsBytes, bytesIsNumber, numberIsNumber, emptyIsEmpty) = Probe();

        await Assert.That(bytesIsBytes).IsTrue();
        await Assert.That(bytesIsNumber).IsFalse();
        await Assert.That(numberIsNumber).IsTrue();
        await Assert.That(emptyIsEmpty).IsTrue();

        static (bool BytesIsBytes, bool BytesIsNumber, bool NumberIsNumber, bool EmptyIsEmpty) Probe()
        {
            Span<byte> data = stackalloc byte[1];
            var bytes = Payload.Bytes(data);
            var number = Payload.Number(42);
            var empty = Payload.Empty();
            return (bytes.IsBytes, bytes.IsNumber, number.IsNumber, empty.IsEmpty);
        }
    }

    [Test]
    public async Task Tag_ReportsActiveVariant()
    {
        await Assert.That(Payload.Number(1).Tag).IsEqualTo(Payload.Tags.Number);
        await Assert.That(Payload.Empty().Tag).IsEqualTo(Payload.Tags.Empty);
        await Assert.That(default(Payload).Tag).IsEqualTo(Payload.Tags.Default);
    }

    [Test]
    public async Task Match_DispatchesOnVariant()
    {
        // Func<Span<byte>, TResult> is legal: the BCL delegates carry `allows ref struct`.
        await Assert.That(Probe()).IsEqualTo(3);

        static int Probe()
        {
            Span<byte> data = stackalloc byte[3];
            return Payload.Bytes(data).Match(
                bytes: b => b.Length,
                text: t => t.Length,
                number: n => n,
                empty: () => -1);
        }
    }

    [Test]
    public async Task Match_SelectsNumberArm()
    {
        var result = Payload.Number(42).Match(
            bytes: b => b.Length,
            text: t => t.Length,
            number: n => n,
            empty: () => -1);

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Match_RunsTheMatchingAction()
    {
        var seen = -1;
        Payload.Number(5).Match(
            bytes: _ => { },
            text: _ => { },
            number: n => seen = n,
            empty: () => { });

        await Assert.That(seen).IsEqualTo(5);
    }

    [Test]
    public async Task Equality_ComparesSpansByIdentityNotContent()
    {
        var (sameBuffer, equalContentDifferentBuffer) = Probe();

        // Span<T>'s operator == is reference equality — same pointer and length. Two spans over
        // distinct buffers holding identical bytes are therefore NOT equal.
        await Assert.That(sameBuffer).IsTrue();
        await Assert.That(equalContentDifferentBuffer).IsFalse();

        static (bool SameBuffer, bool EqualContentDifferentBuffer) Probe()
        {
            var buffer = new byte[] { 1, 2, 3 };
            var copy = new byte[] { 1, 2, 3 };
            return (
                Payload.Bytes(buffer) == Payload.Bytes(buffer),
                Payload.Bytes(buffer) == Payload.Bytes(copy));
        }
    }

    [Test]
    public async Task Equality_DiscriminatesByTag()
    {
        await Assert.That(Payload.Number(1) == Payload.Number(1)).IsTrue();
        await Assert.That(Payload.Number(1) == Payload.Number(2)).IsFalse();
        await Assert.That(Payload.Number(1) != Payload.Empty()).IsTrue();
    }

    [Test]
    public async Task GetHashCode_IsStableAndTagSensitive()
    {
        var (bytesMatchesBytes, numberDiffersFromEmpty) = Probe();

        // The span is skipped by the weak hash, so every Bytes value hashes alike — legal, because
        // equal values must hash equally, not the converse.
        await Assert.That(bytesMatchesBytes).IsTrue();
        await Assert.That(numberDiffersFromEmpty).IsTrue();

        static (bool BytesMatchesBytes, bool NumberDiffersFromEmpty) Probe()
        {
            // Deliberately different lengths and contents — the hash must ignore both.
            var a = new byte[1];
            var b = new byte[2];
            return (
                Payload.Bytes(a).GetHashCode() == Payload.Bytes(b).GetHashCode(),
                Payload.Number(1).GetHashCode() != Payload.Empty().GetHashCode());
        }
    }

    [Test]
    public async Task ToString_RendersSpanContents()
    {
        // ReadOnlySpan<char>.ToString() returns the text itself.
        await Assert.That(Payload.Text("hi".AsSpan()).ToString()).IsEqualTo("Text(hi)");
        await Assert.That(Payload.Number(7).ToString()).IsEqualTo("Number(7)");
        await Assert.That(Payload.Empty().ToString()).IsEqualTo("Empty");
        await Assert.That(default(Payload).ToString()).IsEqualTo("Default");
    }

    [Test]
    public async Task TryGet_ReturnsFalseForTheWrongVariant()
    {
        var (okForRightVariant, okForWrongVariant) = Probe();

        await Assert.That(okForRightVariant).IsTrue();
        await Assert.That(okForWrongVariant).IsFalse();

        static (bool OkForRightVariant, bool OkForWrongVariant) Probe()
        {
            Span<byte> data = stackalloc byte[2];
            var bytes = Payload.Bytes(data);
            return (bytes.TryGetBytes(out _), bytes.TryGetNumber(out _));
        }
    }

    [Test]
    public async Task WrongAccessor_Throws()
    {
        var threw = false;
        try
        {
            _ = Payload.Number(1).BytesData;
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }
}
