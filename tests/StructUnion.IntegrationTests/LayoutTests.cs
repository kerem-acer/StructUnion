using System.Runtime.CompilerServices;

namespace StructUnion.IntegrationTests.Layout;

// A value type holding a reference: cannot overlap anything, so the runtime places it.
public readonly struct Names(string[] items)
{
    public string[] Items { get; } = items;
}

[StructUnion(EnableImplicitConversions = false)]
public readonly partial struct Mixed
{
    public static partial Mixed A(string a);
    public static partial Mixed B(string b);
    public static partial Mixed C(string c);
    public static partial Mixed D(string d);
    public static partial Mixed E(Names names);
    public static partial Mixed N(long low, int high);
}

// Issue #9: a union carrying another union declared in the same compilation.
[StructUnion(EnableImplicitConversions = false)]
public readonly partial struct Inner
{
    public static partial Inner A(string a, int position);
    public static partial Inner B(string b);
    public static partial Inner F(string f, string g);
}

[StructUnion(EnableImplicitConversions = false)]
public readonly partial struct Outer
{
    public static partial Outer A(string a);
    public static partial Outer Nested(Inner inner);
}

// A union with no references of its own: it can share memory with other value fields.
[StructUnion(EnableImplicitConversions = false)]
public readonly partial struct Small
{
    public static partial Small Int(int value);
    public static partial Small Long(long value);
}

[StructUnion(EnableImplicitConversions = false)]
public readonly partial struct HoldsSmall
{
    public static partial HoldsSmall Text(string text);
    public static partial HoldsSmall Number(Small small);
    public static partial HoldsSmall Wide(long low, long high);
}

// A struct generated from a record template is invisible while other unions are generated.
[StructUnion]
public partial record PairRecord
{
    public record Ints(int X, int Y);
    public record Wide(long Z);
}

[StructUnion(EnableImplicitConversions = false)]
public readonly partial struct UsesPair
{
    public static partial UsesPair Text(string text);
    public static partial UsesPair WithPair(Pair pair);
}

// The CLR refuses explicit layout on any generic type, even one whose field sizes are all known.
[StructUnion(EnableImplicitConversions = false)]
public readonly partial struct Box<T>
{
    public static partial Box<T> Items(List<T> items);
    public static partial Box<T> Text(string text);
}

// Common fields stay public fields of the union, beside the storage struct.
[StructUnion]
public partial record TaggedRecord(string Id)
{
    public record Plain(string Text);
    public record Pair((string Name, int Count) Entry);
}

public class LayoutTests
{
    [Test]
    public async Task Hybrid_SharesMemoryBetweenReferenceCases()
    {
        // Sequential layout would put every case side by side: 7 references and 2 numbers.
        await Assert.That(Unsafe.SizeOf<Mixed>()).IsLessThanOrEqualTo(40);
    }

    [Test]
    public async Task Hybrid_WithoutValueCase_IsAsSmallAsHandWritten()
    {
        // The measured size of the layout the generator now emits for #10's example.
        await Assert.That(Unsafe.SizeOf<Hybrid10>()).IsEqualTo(24);
    }

    [Test]
    public async Task Hybrid_ValuesSurviveGarbageCollection()
    {
        var names = Mixed.E(new Names(["x", "y"]));
        var text = Mixed.C("text");
        var number = Mixed.N(long.MaxValue, -1);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(names.ENames.Items[1]).IsEqualTo("y");
        await Assert.That(text.CC).IsEqualTo("text");
        await Assert.That(number.NLow).IsEqualTo(long.MaxValue);
        await Assert.That(number.NHigh).IsEqualTo(-1);
        await Assert.That(text.TryGetA(out _)).IsFalse();
    }

    [Test]
    public async Task Hybrid_EqualityAndMatch()
    {
        var items = new[] { "x" };
        await Assert.That(Mixed.E(new Names(items))).IsEqualTo(Mixed.E(new Names(items)));
        await Assert.That(Mixed.A("s")).IsNotEqualTo(Mixed.B("s"));
        await Assert.That(Mixed.A("s").GetHashCode()).IsEqualTo(Mixed.A("s").GetHashCode());

        var described = Mixed.E(new Names(items)).Match(
            a => a, b => b, c => c, d => d, n => n.Items[0], (low, high) => $"{low}/{high}");
        await Assert.That(described).IsEqualTo("x");
        await Assert.That(Mixed.A("s").ToString()).Contains("s");
    }

    [Test]
    public async Task NestedUnionFromSameCompilation_Loads()
    {
        var outer = Outer.Nested(Inner.F("f", "g"));
        GC.Collect();

        await Assert.That(outer.NestedInner.FF).IsEqualTo("f");
        await Assert.That(outer.NestedInner.FG).IsEqualTo("g");
        await Assert.That(Outer.A("a").AA).IsEqualTo("a");
    }

    [Test]
    public async Task UnmanagedNestedUnion_SharesMemory()
    {
        // Small (16 bytes, no references) shares the value zone with Wide's two longs:
        // tag + padding (8) + string slot (8) + one 16-byte value slot.
        await Assert.That(Unsafe.SizeOf<HoldsSmall>()).IsEqualTo(32);
        await Assert.That(HoldsSmall.Wide(1, 2).WideHigh).IsEqualTo(2L);
        await Assert.That(HoldsSmall.Number(Small.Long(7)).NumberSmall.LongValue).IsEqualTo(7L);
        await Assert.That(HoldsSmall.Text("t").TextText).IsEqualTo("t");
    }

    [Test]
    public async Task TemplateGeneratedStructAsCase_Loads()
    {
        var withPair = UsesPair.WithPair(Pair.Ints(1, 2));
        GC.Collect();

        await Assert.That(withPair.WithPairPair.IntsX).IsEqualTo(1);
        await Assert.That(UsesPair.Text("t").TextText).IsEqualTo("t");
    }

    [Test]
    public async Task GenericUnionWithKnownSizes_Loads()
    {
        var box = Box<int>.Items([1, 2]);
        GC.Collect();

        await Assert.That(box.ItemsItems[1]).IsEqualTo(2);
        await Assert.That(Box<int>.Text("t").TextText).IsEqualTo("t");
    }

    [Test]
    public async Task HybridRecordTemplate_KeepsCommonFields()
    {
        var pair = Tagged.Pair("id", ("n", 3));
        GC.Collect();

        await Assert.That(pair.Id).IsEqualTo("id");
        await Assert.That(pair.PairEntry.Count).IsEqualTo(3);
        await Assert.That(Tagged.Plain("id2", "p").PlainText).IsEqualTo("p");
    }
}

[StructUnion(EnableImplicitConversions = false)]
public readonly partial struct Hybrid10
{
    public static partial Hybrid10 A(string a);
    public static partial Hybrid10 B(string b);
    public static partial Hybrid10 C(string c);
    public static partial Hybrid10 D(string d);
    public static partial Hybrid10 E(Names names);
}
