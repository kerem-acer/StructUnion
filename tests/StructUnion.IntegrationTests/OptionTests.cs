namespace StructUnion.IntegrationTests;

[StructUnion]
public readonly partial struct OptionInt
{
    public static partial OptionInt Some(int value);
    public static partial OptionInt None();
}

public class OptionTests
{
    [Test]
    public async Task Some_IsSome_ReturnsTrue()
    {
        var opt = OptionInt.Some(42);
        await Assert.That(opt.IsSome).IsTrue();
        await Assert.That(opt.IsNone).IsFalse();
    }

    [Test]
    public async Task None_IsNone_ReturnsTrue()
    {
        var opt = OptionInt.None();
        await Assert.That(opt.IsNone).IsTrue();
        await Assert.That(opt.IsSome).IsFalse();
    }

    [Test]
    public async Task Some_PropertyAccess_ReturnsValue()
    {
        var opt = OptionInt.Some(42);
        await Assert.That(opt.SomeValue).IsEqualTo(42);
    }

    [Test]
    public async Task None_Match_CallsNoneBranch()
    {
        var opt = OptionInt.None();
        var result = opt.Match(
            v => v.ToString(),
            () => "nothing");

        await Assert.That(result).IsEqualTo("nothing");
    }

    [Test]
    public async Task Some_Match_CallsSomeBranch()
    {
        var opt = OptionInt.Some(42);
        var result = opt.Match(
            v => v * 2,
            () => -1);

        await Assert.That(result).IsEqualTo(84);
    }

    [Test]
    public async Task TryGetNone_ReturnsTrue()
    {
        var opt = OptionInt.None();
        await Assert.That(opt.TryGetNone()).IsTrue();
    }

    [Test]
    public async Task ImplicitConversion_FromInt()
    {
        OptionInt opt = 42;
        await Assert.That(opt.IsSome).IsTrue();
        await Assert.That(opt.SomeValue).IsEqualTo(42);
    }

    [Test]
    public async Task Equality_TwoNones_AreEqual()
    {
        var a = OptionInt.None();
        var b = OptionInt.None();
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task Equality_SomeAndNone_AreNotEqual()
    {
        var a = OptionInt.Some(42);
        var b = OptionInt.None();
        await Assert.That(a == b).IsFalse();
    }
}
