namespace StructUnion.IntegrationTests.ClassApi;

// Class template with properties as common fields
[StructUnion]
public partial class ShapeRecord
{
    public int Id { get; }

    public class Circle
    {
        public double Radius { get; }
    }

    public class Rectangle
    {
        public double Length { get; }
        public double Width { get; }
    }

    public class Triangle
    {
        public double Base { get; }
        public double Height { get; }
    }
}

public class ClassTemplateTests
{
    [Test]
    public async Task Circle_FactoryIncludesCommonProperty()
    {
        var shape = Shape.Circle(42, 5.0);

        await Assert.That(shape.IsCircle).IsTrue();
        await Assert.That(shape.Id).IsEqualTo(42);
        await Assert.That(shape.CircleRadius).IsEqualTo(5.0);
    }

    [Test]
    public async Task Rectangle_AllFieldsSet()
    {
        var shape = Shape.Rectangle(10, 3.0, 4.0);

        await Assert.That(shape.IsRectangle).IsTrue();
        await Assert.That(shape.Id).IsEqualTo(10);
        await Assert.That(shape.RectangleLength).IsEqualTo(3.0);
        await Assert.That(shape.RectangleWidth).IsEqualTo(4.0);
    }

    [Test]
    public async Task Triangle_Works()
    {
        var shape = Shape.Triangle(0, 6.0, 4.0);
        await Assert.That(shape.IsTriangle).IsTrue();
        await Assert.That(shape.TriangleBase).IsEqualTo(6.0);
        await Assert.That(shape.TriangleHeight).IsEqualTo(4.0);
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
    public async Task Equality_IncludesCommonProperty()
    {
        var a = Shape.Circle(1, 5.0);
        var b = Shape.Circle(2, 5.0);

        await Assert.That(a == b).IsFalse();
    }

    [Test]
    public async Task Equality_SameValues()
    {
        var a = Shape.Rectangle(42, 3.0, 4.0);
        var b = Shape.Rectangle(42, 3.0, 4.0);

        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task EmptyVariant_Class()
    {
        // Variant with no properties = empty variant
        var shape = Shape.Triangle(0, 6.0, 4.0);
        var result = shape.TryGetTriangle(out var b, out _);
        await Assert.That(result).IsTrue();
        await Assert.That(b).IsEqualTo(6.0);
    }
}
