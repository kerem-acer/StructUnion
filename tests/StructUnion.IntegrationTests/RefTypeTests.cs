using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StructUnion.IntegrationTests.RefTypeApi;

[StructUnion]
public partial record PayloadRecord
{
    public record Text(string Value);
    public record Number(int Value);
    public record Both(string Name, int Age);
    public record Empty();
}

public class RefTypeTests
{
    [Test]
    public async Task Text_StoresString()
    {
        var p = Payload.Text("hello");
        await Assert.That(p.IsText).IsTrue();
        await Assert.That(p.TextValue).IsEqualTo("hello");
    }

    [Test]
    public async Task Number_StoresInt()
    {
        var p = Payload.Number(42);
        await Assert.That(p.IsNumber).IsTrue();
        await Assert.That(p.NumberValue).IsEqualTo(42);
    }

    [Test]
    public async Task Both_StoresRefAndValue()
    {
        var p = Payload.Both("alice", 30);
        await Assert.That(p.IsBoth).IsTrue();
        await Assert.That(p.BothName).IsEqualTo("alice");
        await Assert.That(p.BothAge).IsEqualTo(30);
    }

    [Test]
    public async Task Empty_Works()
    {
        var p = Payload.Empty();
        await Assert.That(p.IsEmpty).IsTrue();
        await Assert.That(p.TryGetEmpty()).IsTrue();
    }

    [Test]
    public async Task Match_RefType()
    {
        var p = Payload.Text("world");
        var result = p.Match(
            s => $"text:{s}",
            n => $"num:{n}",
            (name, age) => $"both:{name},{age}",
            () => "empty");
        await Assert.That(result).IsEqualTo("text:world");
    }

    [Test]
    public async Task Equality_RefType()
    {
        var a = Payload.Text("hello");
        var b = Payload.Text("hello");
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task Equality_RefType_Different()
    {
        var a = Payload.Text("hello");
        var b = Payload.Text("world");
        await Assert.That(a == b).IsFalse();
    }

    [Test]
    public async Task Equality_RefVsValue()
    {
        var a = Payload.Text("42");
        var b = Payload.Number(42);
        await Assert.That(a == b).IsFalse();
    }

    [Test]
    public async Task StructLayout_IsExplicit()
    {
        var layout = typeof(Payload).StructLayoutAttribute;
        await Assert.That(layout).IsNotNull();
        await Assert.That(layout!.Value).IsEqualTo(LayoutKind.Explicit);
    }

    [Test]
    public async Task StructSize_IsCompact()
    {
        // tag(1) + int(4 @4) + string(8 @8) = 16 bytes
        // value fields packed into gap before ref zone
        var size = Unsafe.SizeOf<Payload>();
        await Assert.That(size).IsEqualTo(16);
    }

    [Test]
    public async Task TryGet_WrongVariant()
    {
        var p = Payload.Text("hello");
        var result = p.TryGetNumber(out var value);
        await Assert.That(result).IsFalse();
        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task ToString_RefType()
    {
        var p = Payload.Text("hello");
        await Assert.That(p.ToString()).IsEqualTo("Text(hello)");
    }

    [Test]
    public async Task Null_RefValue()
    {
        var p = Payload.Text(null!);
        await Assert.That(p.IsText).IsTrue();
        await Assert.That(p.TextValue).IsNull();
    }
}
