using System.Runtime.InteropServices;

namespace StructUnion.IntegrationTests.RecordApi;

[StructUnion]
public partial record ShapeRecord(int Common)
{
    public record Circle(double Radius);
    public record Rectangle(double Length, double Width);
    public record Triangle(double Base, double Height);
}

public class RecordTemplateTests
{
    [Test]
    public async Task Circle_FactoryIncludesCommonField()
    {
        // Factory methods include common field as first parameter
        var shape = Shape.Circle(42, 5.0);

        await Assert.That(shape.IsCircle).IsTrue();
        await Assert.That(shape.Common).IsEqualTo(42);
        await Assert.That(shape.CircleRadius).IsEqualTo(5.0);
    }

    [Test]
    public async Task Rectangle_AllFieldsSet()
    {
        var shape = Shape.Rectangle(10, 3.0, 4.0);

        await Assert.That(shape.IsRectangle).IsTrue();
        await Assert.That(shape.Common).IsEqualTo(10);
        await Assert.That(shape.RectangleLength).IsEqualTo(3.0);
        await Assert.That(shape.RectangleWidth).IsEqualTo(4.0);
    }

    [Test]
    public async Task Match_Works()
    {
        var shape = Shape.Circle(0, 5.0);
        var result = shape.Match(
            r => $"circle:{r}",
            (l, w) => $"rect:{l}x{w}",
            (b, h) => $"tri:{b}x{h}");

        await Assert.That(result).IsEqualTo("circle:5");
    }

    [Test]
    public async Task Equality_IncludesCommonField()
    {
        var a = Shape.Circle(1, 5.0);
        var b = Shape.Circle(2, 5.0);

        await Assert.That(a == b).IsFalse(); // different common field
    }

    [Test]
    public async Task Equality_SameValues()
    {
        var a = Shape.Circle(42, 5.0);
        var b = Shape.Circle(42, 5.0);

        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task StructLayout_IsExplicit()
    {
        var layout = typeof(Shape).StructLayoutAttribute;
        await Assert.That(layout).IsNotNull();
        await Assert.That(layout!.Value).IsEqualTo(LayoutKind.Explicit);
    }

    [Test]
    public async Task ToString_FormatsCorrectly()
    {
        var shape = Shape.Circle(42, 5.0);
        await Assert.That(shape.ToString()).IsEqualTo("Circle(5)");
    }

    [Test]
    public async Task TryGet_Works()
    {
        var shape = Shape.Triangle(0, 6.0, 4.0);
        var result = shape.TryGetTriangle(out var b, out var h);

        await Assert.That(result).IsTrue();
        await Assert.That(b).IsEqualTo(6.0);
        await Assert.That(h).IsEqualTo(4.0);
    }

    [Test]
    public async Task DefaultStruct_IsNoVariant()
    {
        var shape = default(Shape);
        await Assert.That(shape.IsCircle).IsFalse();
        await Assert.That(shape.IsRectangle).IsFalse();
        await Assert.That(shape.IsTriangle).IsFalse();
        await Assert.That(shape.Common).IsEqualTo(0);
    }
}
