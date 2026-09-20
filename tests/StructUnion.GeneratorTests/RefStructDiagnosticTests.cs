namespace StructUnion.GeneratorTests;

/// <summary>
/// SU0018 / SU0019 / SU0020 — the ref-struct diagnostics.
/// </summary>
public class RefStructDiagnosticTests
{
    // -- SU0018: ref-like field without a ref declaration --

    [Test]
    public async Task SpanFieldOnNonRefStruct_ReportsSU0018()
    {
        var source = """
            using System;
            using StructUnion;

            [StructUnion]
            public readonly partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
                public static partial Payload Number(int value);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0018");

        // Must abort: emitting the struct without `ref` makes every ref-like field a CS8345, and the
        // user would get a pile of errors inside generated code instead of one at their declaration.
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    public async Task UserRefStructFieldOnNonRefStruct_ReportsSU0018()
    {
        var source = """
            using StructUnion;

            public ref struct Plain { public int X; }

            [StructUnion]
            public readonly partial struct Holder
            {
                public static partial Holder A(Plain p);
                public static partial Holder B(int n);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0018");
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    public async Task GenericAllowsRefStructOnNonRefStruct_ReportsSU0018()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Box<T> where T : allows ref struct
            {
                public static partial Box<T> Some(T value);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0018");
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    public async Task RefDeclaredSpanUnion_DoesNotReportSU0018()
    {
        var source = """
            using System;
            using StructUnion;

            [StructUnion]
            public readonly ref partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
                public static partial Payload Number(int value);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task RefOnASeparatePartialDeclaration_DoesNotReportSU0018()
    {
        // The modifier is read from the symbol, not the syntax carrying [StructUnion], so `ref` on
        // any of the user's partial declarations counts.
        var source = """
            using System;
            using StructUnion;

            public readonly ref partial struct Payload;

            [StructUnion]
            public readonly partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
                public static partial Payload Number(int value);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0018");
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task OrdinaryUnion_DoesNotReportSU0018()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Square(double side);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0018");
    }

    // -- SU0019: ref-like field that cannot be compared --

    const string NonComparableRefStruct = """
        using System;
        using StructUnion;

        // No operator == and no IEquatable<Buffer>, so nothing can compare it: a ref struct cannot be
        // a generic type argument to EqualityComparer<T>, nor boxed to reach object.Equals.
        public ref struct Buffer { public Span<byte> Data; }

        [StructUnion]
        public readonly ref partial struct Holder
        {
            public static partial Holder Buf(Buffer buffer);
            public static partial Holder Number(int value);
        }
        """;

    [Test]
    public async Task NonComparableRefStructField_ReportsSU0019()
    {
        var result = GeneratorTestHelper.CreateDriver(NonComparableRefStruct).GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0019");

        // Degrades rather than aborting — the union is still useful without equality.
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
    }

    [Test]
    public void NonComparableRefStructField_StillCompiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles(NonComparableRefStruct);

    [Test]
    public async Task NonComparableRefStructField_OmitsEqualityMembers()
    {
        var result = GeneratorTestHelper.CreateDriver(NonComparableRefStruct).GetRunResult();
        var generated = string.Concat(result.GeneratedTrees.Select(t => t.ToString()));

        await Assert.That(generated).DoesNotContain("public bool Equals(Holder other)");
        await Assert.That(generated).DoesNotContain("operator ==");
        await Assert.That(generated).DoesNotContain("IEquatable<Holder>");

        // GetHashCode is independent of Equals, so a weak hash over the tag survives.
        await Assert.That(generated).Contains("public override int GetHashCode()");
    }

    [Test]
    public async Task SpanField_DoesNotReportSU0019()
    {
        // Span<T> declares operator ==, so it is comparable even though it has no IEquatable.
        var source = """
            using System;
            using StructUnion;

            [StructUnion]
            public readonly ref partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0019");
    }

    [Test]
    public async Task RefStructWithIEquatable_DoesNotReportSU0019()
    {
        var source = """
            using System;
            using StructUnion;

            public ref struct Buffer : IEquatable<Buffer>
            {
                public int Length;
                public bool Equals(Buffer other) => Length == other.Length;
            }

            [StructUnion]
            public readonly ref partial struct Holder
            {
                public static partial Holder Buf(Buffer buffer);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0019");
        GeneratorTestHelper.AssertGeneratedCompiles(source);
    }

    [Test]
    public async Task NonComparableRefStructWithUserEquals_DoesNotReportSU0019()
    {
        // Writing the equality yourself is a valid remedy, so warning about the field would be noise.
        var source = """
            using System;
            using StructUnion;

            public ref struct Buffer { public Span<byte> Data; }

            [StructUnion]
            public readonly ref partial struct Holder
            {
                public static partial Holder Buf(Buffer buffer);
                public static partial Holder Number(int value);

                public bool Equals(Holder other) => _tag == other._tag;
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0019");
    }

    [Test]
    public async Task GenerateEqualityFalse_DoesNotReportSU0019()
    {
        var source = """
            using System;
            using StructUnion;

            public ref struct Buffer { public Span<byte> Data; }

            [StructUnion(GenerateEquality = false)]
            public readonly ref partial struct Holder
            {
                public static partial Holder Buf(Buffer buffer);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0019");
    }

    // -- SU0020: NativeUnion with a ref-like field --

    [Test]
    public async Task NativeUnionWithSpanField_ReportsSU0020()
    {
        var source = """
            using System;
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly ref partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
                public static partial Payload Number(int value);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true).GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0020");

        // Same shape as SU0016: reported, but generation continues without the union members so a
        // single-knob mistake does not cascade into errors all over the user's code.
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);

        var generated = string.Concat(result.GeneratedTrees.Select(t => t.ToString()));
        await Assert.That(generated).DoesNotContain("[global::System.Runtime.CompilerServices.Union]");
        await Assert.That(generated).DoesNotContain("IUnionMembers");
    }

    [Test]
    public void NativeUnionWithSpanField_StillCompiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles(
            """
            using System;
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly ref partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
                public static partial Payload Number(int value);
            }
            """,
            includeUnionRuntime: true);

    [Test]
    public async Task NativeUnionWithoutRefStructField_DoesNotReportSU0020()
    {
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Square(double side);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true).GetRunResult();

        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0020");
    }
}
