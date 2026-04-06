using System.Runtime.CompilerServices;

namespace StructUnion.IntegrationTests.ComplexTypes;

[StructUnion]
public readonly partial struct SmallUnion
{
    public static partial SmallUnion ByteCase(byte value);
    public static partial SmallUnion BoolCase(bool value);
}

public class SmallUnionTests
{
    [Test]
    public async Task SmallUnion_Byte()
    {
        var u = SmallUnion.ByteCase(0xFF);
        await Assert.That(u.IsByteCase).IsTrue();
        await Assert.That(u.ByteCaseValue).IsEqualTo((byte)0xFF);
    }

    [Test]
    public async Task SmallUnion_Bool()
    {
        var u = SmallUnion.BoolCase(true);
        await Assert.That(u.IsBoolCase).IsTrue();
        await Assert.That(u.BoolCaseValue).IsTrue();
    }

    [Test]
    public async Task SmallUnion_Size()
    {
        // tag(1) + byte/bool(1) = 2 bytes, alignment 1
        var size = Unsafe.SizeOf<SmallUnion>();
        await Assert.That(size).IsEqualTo(2);
    }

    [Test]
    public async Task SmallUnion_TryGet()
    {
        var u = SmallUnion.ByteCase(42);
        await Assert.That(u.TryGetBoolCase(out _)).IsFalse();

        var ok = u.TryGetByteCase(out var val);
        await Assert.That(ok).IsTrue();
        await Assert.That(val).IsEqualTo((byte)42);
    }
}
