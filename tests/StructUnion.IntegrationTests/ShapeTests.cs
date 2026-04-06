using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StructUnion.IntegrationTests;

[StructUnion]
public readonly partial struct Shape
{
    public static partial Shape Circle(double radius);
    public static partial Shape Rectangle(double length, double width);
    public static partial Shape Triangle(double @base, double height);
}

public class ShapeTests
{
    [Test]
    public async Task Circle_IsCircle_ReturnsTrue()
    {
        var shape = Shape.Circle(5.0);
        await Assert.That(shape.IsCircle).IsTrue();
        await Assert.That(shape.IsRectangle).IsFalse();
        await Assert.That(shape.IsTriangle).IsFalse();
    }

    [Test]
    public async Task Circle_PropertyAccess_ReturnsValue()
    {
        var shape = Shape.Circle(5.0);
        await Assert.That(shape.CircleRadius).IsEqualTo(5.0);
    }

    [Test]
    public async Task Circle_WrongPropertyAccess_Throws()
    {
        var shape = Shape.Circle(5.0);
        var threw = false;
        try
        { _ = shape.RectangleLength; }
        catch (InvalidOperationException) { threw = true; }
        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task Match_ReturnsCorrectResult()
    {
        var shape = Shape.Circle(5.0);
        var area = shape.Match(
            r => Math.PI * r * r,
            (l, w) => l * w,
            (b, h) => 0.5 * b * h);

        await Assert.That(area).IsEqualTo(Math.PI * 25.0);
    }

    [Test]
    public async Task Match_Rectangle_ReturnsCorrectResult()
    {
        var shape = Shape.Rectangle(3.0, 4.0);
        var area = shape.Match(
            r => Math.PI * r * r,
            (l, w) => l * w,
            (b, h) => 0.5 * b * h);

        await Assert.That(area).IsEqualTo(12.0);
    }

    [Test]
    public async Task Match_Action_CallsCorrectBranch()
    {
        var shape = Shape.Triangle(6.0, 4.0);
        var called = "";
        shape.Match(
            r => called = "circle",
            (l, w) => called = "rectangle",
            (b, h) => called = "triangle");

        await Assert.That(called).IsEqualTo("triangle");
    }

    [Test]
    public async Task TryGet_CorrectVariant_ReturnsTrue()
    {
        var shape = Shape.Circle(5.0);
        var result = shape.TryGetCircle(out var radius);

        await Assert.That(result).IsTrue();
        await Assert.That(radius).IsEqualTo(5.0);
    }

    [Test]
    public async Task TryGet_WrongVariant_ReturnsFalse()
    {
        var shape = Shape.Circle(5.0);
        var result = shape.TryGetRectangle(out var length, out var width);

        await Assert.That(result).IsFalse();
        await Assert.That(length).IsEqualTo(0.0);
        await Assert.That(width).IsEqualTo(0.0);
    }

    [Test]
    public async Task Equality_SameVariantSameData_AreEqual()
    {
        var a = Shape.Circle(5.0);
        var b = Shape.Circle(5.0);

        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a == b).IsTrue();
        await Assert.That(a != b).IsFalse();
    }

    [Test]
    public async Task Equality_SameVariantDifferentData_AreNotEqual()
    {
        var a = Shape.Circle(5.0);
        var b = Shape.Circle(10.0);

        await Assert.That(a == b).IsFalse();
    }

    [Test]
    public async Task Equality_DifferentVariants_AreNotEqual()
    {
        var a = Shape.Circle(5.0);
        var b = Shape.Rectangle(5.0, 5.0);

        await Assert.That(a == b).IsFalse();
    }

    [Test]
    public async Task ToString_FormatsCorrectly()
    {
        var circle = Shape.Circle(5.0);
        var rect = Shape.Rectangle(3.0, 4.0);

        await Assert.That(circle.ToString()).IsEqualTo("Circle(5)");
        await Assert.That(rect.ToString()).IsEqualTo("Rectangle(3, 4)");
    }

    [Test]
    public async Task DefaultStruct_IsNoVariant()
    {
        var shape = default(Shape);
        await Assert.That(shape.IsCircle).IsFalse();
        await Assert.That(shape.IsRectangle).IsFalse();
        await Assert.That(shape.IsTriangle).IsFalse();
    }

    [Test]
    public async Task DefaultStruct_IsDefault_ReturnsTrue()
    {
        var shape = default(Shape);
        await Assert.That(shape.IsDefault).IsTrue();
    }

    [Test]
    public async Task NonDefault_IsDefault_ReturnsFalse()
    {
        var shape = Shape.Circle(5.0);
        await Assert.That(shape.IsDefault).IsFalse();
    }

    [Test]
    public async Task DefaultStruct_Match_Throws()
    {
        var shape = default(Shape);
        var threw = false;
        try
        { shape.Match(r => { }, (l, w) => { }, (b, h) => { }); }
        catch (InvalidOperationException) { threw = true; }
        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task StructLayout_IsExplicit()
    {
        var layout = typeof(Shape).StructLayoutAttribute;
        await Assert.That(layout).IsNotNull();
        await Assert.That(layout!.Value).IsEqualTo(LayoutKind.Explicit);
    }

    [Test]
    public async Task StructSize_IsCompact()
    {
        // tag(1 byte) + padding(7 bytes) + max variant data (2 doubles = 16 bytes) = 24 bytes
        var size = Unsafe.SizeOf<Shape>();
        await Assert.That(size).IsEqualTo(24);
    }

    [Test]
    public async Task Triangle_KeywordParam_Works()
    {
        var shape = Shape.Triangle(6.0, 4.0);
        await Assert.That(shape.IsTriangle).IsTrue();
        await Assert.That(shape.TriangleBase).IsEqualTo(6.0);
        await Assert.That(shape.TriangleHeight).IsEqualTo(4.0);
    }

    [Test]
    public async Task Tag_ReturnsCorrectEnumValue()
    {
        var circle = Shape.Circle(5.0);
        var rect = Shape.Rectangle(3.0, 4.0);
        var tri = Shape.Triangle(6.0, 4.0);

        await Assert.That(circle.Tag).IsEqualTo(Shape.Tags.Circle);
        await Assert.That(rect.Tag).IsEqualTo(Shape.Tags.Rectangle);
        await Assert.That(tri.Tag).IsEqualTo(Shape.Tags.Triangle);
    }

    [Test]
    public async Task Tag_Default_ReturnsDefault()
    {
        var shape = default(Shape);
        await Assert.That(shape.Tag).IsEqualTo(Shape.Tags.Default);
    }

    [Test]
    public async Task Tag_SwitchExpression_Works()
    {
        var shape = Shape.Circle(5.0);
        var name = shape.Tag switch
        {
            Shape.Tags.Circle => "circle",
            Shape.Tags.Rectangle => "rectangle",
            Shape.Tags.Triangle => "triangle",
            _ => "unknown"
        };

        await Assert.That(name).IsEqualTo("circle");
    }
}
