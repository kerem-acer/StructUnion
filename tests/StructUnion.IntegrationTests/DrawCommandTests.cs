using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StructUnion.IntegrationTests.ComplexTypes;

[StructUnion]
public readonly partial struct DrawCommand
{
    public static partial DrawCommand MoveTo(Point point);
    public static partial DrawCommand LineTo(Point point);
    public static partial DrawCommand SetColor(Color color);
    public static partial DrawCommand SetWidth(float width);
}

public class DrawCommandTests
{
    [Test]
    public async Task DrawCommand_UserStruct()
    {
        var cmd = DrawCommand.MoveTo(new Point { X = 1.5, Y = 2.5 });
        await Assert.That(cmd.IsMoveTo).IsTrue();
        await Assert.That(cmd.MoveToPoint.X).IsEqualTo(1.5);
        await Assert.That(cmd.MoveToPoint.Y).IsEqualTo(2.5);
    }

    [Test]
    public async Task DrawCommand_Enum()
    {
        var cmd = DrawCommand.SetColor(Color.Blue);
        await Assert.That(cmd.IsSetColor).IsTrue();
        await Assert.That(cmd.SetColorColor).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task DrawCommand_Match()
    {
        var cmd = DrawCommand.LineTo(new Point { X = 10, Y = 20 });
        var result = cmd.Match(
            p => $"move:{p.X},{p.Y}",
            p => $"line:{p.X},{p.Y}",
            c => $"color:{c}",
            w => $"width:{w}");
        await Assert.That(result).IsEqualTo("line:10,20");
    }

    [Test]
    public async Task DrawCommand_Equality_UserStruct()
    {
        var a = DrawCommand.MoveTo(new Point { X = 1, Y = 2 });
        var b = DrawCommand.MoveTo(new Point { X = 1, Y = 2 });
        var c = DrawCommand.MoveTo(new Point { X = 3, Y = 4 });

        await Assert.That(a == b).IsTrue();
        await Assert.That(a == c).IsFalse();
    }

    [Test]
    public async Task DrawCommand_StructLayout()
    {
        var layout = typeof(DrawCommand).StructLayoutAttribute;
        await Assert.That(layout).IsNotNull();
        await Assert.That(layout!.Value).IsEqualTo(LayoutKind.Explicit);
    }

    [Test]
    public async Task DrawCommand_Size()
    {
        // Point is 16 bytes (2 doubles), largest variant
        // tag(1) + padding(7) + Point(16) = 24
        var size = Unsafe.SizeOf<DrawCommand>();
        await Assert.That(size).IsEqualTo(24);
    }
}
