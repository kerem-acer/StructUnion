namespace StructUnion.IntegrationTests.NestedApi;

[StructUnion(NestedAccessors = true)]
public readonly partial struct DrawCmd
{
    public static partial DrawCmd MoveTo(double x, double y);
    public static partial DrawCmd LineTo(double x, double y);
    public static partial DrawCmd Close();
}

[StructUnion(NestedAccessors = true)]
public partial record TokenRecord
{
    public record Identifier(string Name);
    public record Number(double Value);
    public record Operator(string Symbol);
    public record Eof();
}

public class NestedAccessorTests
{
    [Test]
    public async Task AsVariant_ReturnsCorrectData()
    {
        var cmd = DrawCmd.MoveTo(10, 20);
        var move = cmd.AsMoveTo;

        await Assert.That(move.X).IsEqualTo(10.0);
        await Assert.That(move.Y).IsEqualTo(20.0);
    }

    [Test]
    public async Task AsVariant_WrongVariant_Throws()
    {
        var cmd = DrawCmd.Close();
        var threw = false;
        try
        { _ = cmd.AsMoveTo; }
        catch (InvalidOperationException) { threw = true; }
        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task AsVariant_DuplicateFieldNames_Work()
    {
        var move = DrawCmd.MoveTo(1, 2);
        var line = DrawCmd.LineTo(3, 4);

        // Both variants have X and Y — nested accessors allow this
        await Assert.That(move.AsMoveTo.X).IsEqualTo(1.0);
        await Assert.That(move.AsMoveTo.Y).IsEqualTo(2.0);
        await Assert.That(line.AsLineTo.X).IsEqualTo(3.0);
        await Assert.That(line.AsLineTo.Y).IsEqualTo(4.0);
    }

    [Test]
    public async Task TryGet_ReturnsVariantStruct()
    {
        var cmd = DrawCmd.MoveTo(5, 6);
        var result = cmd.TryGetMoveTo(out var data);

        await Assert.That(result).IsTrue();
        await Assert.That(data.X).IsEqualTo(5.0);
        await Assert.That(data.Y).IsEqualTo(6.0);
    }

    [Test]
    public async Task TryGet_WrongVariant_ReturnsFalse()
    {
        var cmd = DrawCmd.Close();
        var result = cmd.TryGetMoveTo(out var data);

        await Assert.That(result).IsFalse();
        await Assert.That(data).IsEqualTo(default);
    }

    [Test]
    public async Task TryGet_EmptyVariant_StillWorks()
    {
        var cmd = DrawCmd.Close();
        await Assert.That(cmd.TryGetClose()).IsTrue();
        await Assert.That(cmd.TryGetMoveTo(out _)).IsFalse();
    }

    [Test]
    public async Task IsCheck_StillWorks()
    {
        var cmd = DrawCmd.LineTo(1, 2);
        await Assert.That(cmd.IsLineTo).IsTrue();
        await Assert.That(cmd.IsMoveTo).IsFalse();
        await Assert.That(cmd.IsClose).IsFalse();
    }

    [Test]
    public async Task Match_StillWorks()
    {
        var cmd = DrawCmd.MoveTo(3, 4);
        var result = cmd.Match(
            (x, y) => $"move({x},{y})",
            (x, y) => $"line({x},{y})",
            () => "close");

        await Assert.That(result).IsEqualTo("move(3,4)");
    }

    [Test]
    public async Task RecordTemplate_NestedAccessors()
    {
        var tok = Token.Identifier("foo");
        var id = tok.AsIdentifier;

        await Assert.That(id.Name).IsEqualTo("foo");
    }

    [Test]
    public async Task RecordTemplate_TryGet_NestedAccessors()
    {
        var tok = Token.Number(42);
        var result = tok.TryGetNumber(out var data);

        await Assert.That(result).IsTrue();
        await Assert.That(data.Value).IsEqualTo(42.0);
    }

    [Test]
    public async Task Equality_StillWorks()
    {
        var a = DrawCmd.MoveTo(1, 2);
        var b = DrawCmd.MoveTo(1, 2);
        var c = DrawCmd.MoveTo(3, 4);

        await Assert.That(a == b).IsTrue();
        await Assert.That(a == c).IsFalse();
    }

    [Test]
    public async Task ToString_StillWorks()
    {
        var cmd = DrawCmd.MoveTo(1, 2);
        await Assert.That(cmd.ToString()).IsEqualTo("MoveTo(1, 2)");
    }

    [Test]
    public async Task CaseStruct_Deconstructs()
    {
        var (x, y) = DrawCmd.MoveTo(10, 20).AsMoveTo;

        await Assert.That(x).IsEqualTo(10.0);
        await Assert.That(y).IsEqualTo(20.0);
    }

    [Test]
    public async Task CaseStruct_SupportsPositionalPattern()
    {
        var cmd = DrawCmd.LineTo(3, 4);

        var length = cmd.TryGetLineTo(out var line) && line is (var x, var y)
            ? Math.Sqrt((x * x) + (y * y))
            : 0;

        await Assert.That(length).IsEqualTo(5.0);
    }

    [Test]
    public async Task CaseStruct_FromRecordTemplate_Deconstructs()
    {
        // A one-element deconstruction has no `var (x)` form, so call it directly.
        Token.Identifier("total").AsIdentifier.Deconstruct(out var name);

        await Assert.That(name).IsEqualTo("total");
    }
}
