using System.Runtime.CompilerServices;
using static StructUnion.NativeUnionTests.Shape.Cases;

namespace StructUnion.NativeUnionTests;

/// <summary>
/// Behavioural proof that the generated union members satisfy the C# 15 union pattern. The
/// snapshot tests pin what is emitted; only compiling and running it against the real compiler
/// and runtime proves the compiler accepts the shape and takes the non-boxing path.
/// </summary>
public class NativeUnionTests
{
    // `using static Shape.Cases` imports the nested case types, so arms read as bare names —
    // the same shape as a switch over the language's own `union Pet(Cat, Dog, Bird)`.
    static double Area(Shape shape) => shape switch
    {
        Circle c => Math.PI * c.Radius * c.Radius,
        Rectangle(var length, var width) => length * width,
        Empty => 0,
        null => -1,
    };

    [Test]
    public async Task TypePattern_BindsCaseType()
    {
        var shape = Shape.Circle(5);

        await Assert.That(shape is Circle).IsTrue();
        await Assert.That(shape is Rectangle).IsFalse();

        if (shape is Circle circle)
        {
            await Assert.That(circle.Radius).IsEqualTo(5.0);
        }
    }

    /// <summary>
    /// The assertion here is that this file compiles at all: <see cref="Area"/> has no discard arm,
    /// so the compiler had to accept the case types as exhausting the union. Adding a variant to
    /// <see cref="Shape"/> without updating <see cref="Area"/> turns this into a build warning,
    /// and <c>TreatWarningsAsErrors</c> turns that into a build failure.
    /// </summary>
    [Test]
    public async Task ExhaustiveSwitch_NeedsNoDiscardArm()
    {
        await Assert.That(Area(Shape.Rectangle(3, 4))).IsEqualTo(12.0);
        await Assert.That(Area(Shape.Circle(1))).IsEqualTo(Math.PI);
        await Assert.That(Area(Shape.Empty())).IsEqualTo(0);
    }

    [Test]
    public async Task PositionalPattern_Deconstructs()
    {
        var shape = Shape.Rectangle(3, 4);

        var perimeter = shape switch
        {
            Rectangle(var length, var width) => 2 * (length + width),
            Circle c => 2 * Math.PI * c.Radius,
            Empty => 0,
            null => -1,
        };

        await Assert.That(perimeter).IsEqualTo(14.0);
    }

    [Test]
    public async Task DefaultInstance_MatchesNull()
    {
        await Assert.That(default(Shape) is null).IsTrue();
        await Assert.That(Shape.Circle(1) is null).IsFalse();
    }

    /// <summary>
    /// The null arm in <see cref="Area"/> is not required by the compiler — every case type is a
    /// non-nullable struct, so Value's default null state is "not null". Without it, a default
    /// instance would fall off the end of an otherwise-exhaustive switch.
    /// </summary>
    [Test]
    public async Task DefaultInstance_FallsToNullArm()
    {
        await Assert.That(Area(default)).IsEqualTo(-1);
    }

    [Test]
    public async Task ExhaustiveSwitchWithoutNullArm_ThrowsOnDefault()
    {
        static double AreaWithoutNullArm(Shape shape) => shape switch
        {
            Circle c => Math.PI * c.Radius * c.Radius,
            Rectangle(var length, var width) => length * width,
            Empty => 0,
        };

        await Assert.That(() => AreaWithoutNullArm(default))
            .Throws<SwitchExpressionException>();
    }

    [Test]
    public async Task UnionConversion_FromCaseType()
    {
        Shape shape = new Rectangle(2, 3);

        await Assert.That(shape.IsRectangle).IsTrue();
        await Assert.That(shape.RectangleLength).IsEqualTo(2.0);
        await Assert.That(shape.RectangleWidth).IsEqualTo(3.0);
    }

    [Test]
    public async Task IUnion_ExposesValueAtRuntime()
    {
        object boxed = Shape.Circle(7);

        await Assert.That(boxed is IUnion).IsTrue();
        await Assert.That(((IUnion)boxed).Value is Circle { Radius: 7 }).IsTrue();
        await Assert.That(((IUnion)(object)default(Shape)).Value).IsNull();
    }

    [Test]
    public async Task BoxedValue_IsCachedForEmptyVariants()
    {
        var shape = Shape.Empty();

        await Assert.That(ReferenceEquals(((IUnion)shape).Value, ((IUnion)shape).Value)).IsTrue();
    }

    [Test]
    public async Task GenericUnion_Matches()
    {
        var some = Option<int>.Some(42);
        var none = Option<int>.None();

        static string Describe(Option<int> option) => option switch
        {
            Option<int>.Cases.Some s => $"some {s.Value}",
            Option<int>.Cases.None => "none",
            null => "default",
        };

        await Assert.That(Describe(some)).IsEqualTo("some 42");
        await Assert.That(Describe(none)).IsEqualTo("none");
        await Assert.That(Describe(default)).IsEqualTo("default");
    }

    [Test]
    public async Task VariantNamedValue_DoesNotCollideWithUnionMembers()
    {
        var awkward = Awkward.Value(3);

        await Assert.That(awkward is Awkward.Cases.Value).IsTrue();
        await Assert.That(awkward.ValueX).IsEqualTo(3);
        await Assert.That(awkward.TryGetValue(out int x)).IsTrue();
        await Assert.That(x).IsEqualTo(3);
    }

    [Test]
    public async Task ExistingApi_StillWorks()
    {
        var shape = Shape.Rectangle(3, 4);

        await Assert.That(shape.Tag).IsEqualTo(Shape.Tags.Rectangle);
        await Assert.That(shape.IsRectangle).IsTrue();
        await Assert.That(shape.IsDefault).IsFalse();
        await Assert.That(shape.TryGetRectangle(out var length, out var width)).IsTrue();
        await Assert.That(length).IsEqualTo(3.0);
        await Assert.That(shape.Match(r => r, (l, w) => l * w, () => 0.0)).IsEqualTo(12.0);
        await Assert.That(shape).IsEqualTo(Shape.Rectangle(3, 4));
        await Assert.That(shape.ToString()).IsEqualTo("Rectangle(3, 4)");
    }

    /// <summary>
    /// The headline claim. If the compiler fell back to the object-typed Value property instead of
    /// calling TryGetValue, every match would box its case struct and this would allocate.
    /// </summary>
    [Test]
    public async Task PatternMatching_DoesNotAllocate()
    {
        Shape[] shapes = [Shape.Circle(1), Shape.Rectangle(2, 3), Shape.Empty(), default];

        // Warm up: JIT the switch and the assertion path before measuring.
        var warmup = 0.0;
        for (var i = 0; i < 1_000; i++)
        {
            warmup += MatchAll(shapes);
        }

        await Assert.That(warmup).IsNotEqualTo(double.NaN);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = MatchAll(shapes);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(total).IsNotEqualTo(double.NaN);
        await Assert.That(allocated).IsEqualTo(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static double MatchAll(Shape[] shapes)
    {
        var total = 0.0;
        foreach (var shape in shapes)
        {
            total += Area(shape);
        }

        return total;
    }
}
