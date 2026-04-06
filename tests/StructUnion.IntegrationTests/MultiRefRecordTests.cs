using System.Runtime.InteropServices;

namespace StructUnion.IntegrationTests.ComplexTypes;

/// <summary>
/// test test
/// </summary>
[StructUnion]
public partial record MultiRefRecord
{
    public record TwoStrings(string First, string Second);
    public record StringAndArray(string Name, int[] Data);
    public record ThreeRefs(string A, object B, int[] C);
}

public class MultiRefRecordTests
{
    [Test]
    public async Task MultiRef_TwoStrings()
    {
        var v = MultiRef.TwoStrings("a", "b");
        await Assert.That(v.IsTwoStrings).IsTrue();
        await Assert.That(v.TwoStringsFirst).IsEqualTo("a");
        await Assert.That(v.TwoStringsSecond).IsEqualTo("b");
    }

    [Test]
    public async Task MultiRef_StringAndArray()
    {
        var arr = new[] { 1, 2, 3 };
        var v = MultiRef.StringAndArray("test", arr);
        await Assert.That(v.StringAndArrayName).IsEqualTo("test");
        await Assert.That(v.StringAndArrayData).IsSameReferenceAs(arr);
    }

    [Test]
    public async Task MultiRef_ThreeRefs()
    {
        var obj = new object();
        var arr = new[] { 1 };
        var v = MultiRef.ThreeRefs("x", obj, arr);
        await Assert.That(v.ThreeRefsA).IsEqualTo("x");
        await Assert.That(v.ThreeRefsB).IsSameReferenceAs(obj);
        await Assert.That(v.ThreeRefsC).IsSameReferenceAs(arr);
    }

    [Test]
    public async Task MultiRef_ExplicitLayout()
    {
        var layout = typeof(MultiRef).StructLayoutAttribute;
        await Assert.That(layout).IsNotNull();
        await Assert.That(layout!.Value).IsEqualTo(LayoutKind.Explicit);
    }

    [Test]
    public async Task MultiRef_Equality()
    {
        var a = MultiRef.TwoStrings("hello", "world");
        var b = MultiRef.TwoStrings("hello", "world");
        var c = MultiRef.TwoStrings("hello", "other");
        await Assert.That(a == b).IsTrue();
        await Assert.That(a == c).IsFalse();
    }
}
