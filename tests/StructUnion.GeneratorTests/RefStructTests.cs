using Microsoft.CodeAnalysis;

namespace StructUnion.GeneratorTests;

/// <summary>
/// Unions carrying ref-like fields (<c>Span&lt;T&gt;</c>, a user <c>ref struct</c>, or a type
/// parameter declared <c>allows ref struct</c>).
/// </summary>
public class RefStructTests
{
    const string SpanUnion = """
        using System;
        using StructUnion;

        [StructUnion]
        public readonly ref partial struct Payload
        {
            public static partial Payload Bytes(Span<byte> data);
            public static partial Payload Number(int value);
            public static partial Payload Empty();
        }
        """;

    // -- Generated code compiles --

    [Test]
    public void SpanUnion_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles(SpanUnion);

    [Test]
    public void ReadOnlySpanUnion_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using System;
            using StructUnion;

            [StructUnion]
            public readonly ref partial struct Token
            {
                public static partial Token Text(ReadOnlySpan<char> text);
                public static partial Token Number(int value);
            }
            """);

    [Test]
    public void NestedAccessors_CaseStructIsRef_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using System;
            using StructUnion;

            [StructUnion(NestedAccessors = true)]
            public readonly ref partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
                public static partial Payload Number(int value);
                public static partial Payload Empty();
            }
            """);

    [Test]
    public void MultipleRefLikeFieldsInOneVariant_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using System;
            using StructUnion;

            [StructUnion]
            public readonly ref partial struct Pair
            {
                public static partial Pair Both(Span<byte> left, ReadOnlySpan<char> right);
                public static partial Pair Number(int value);
            }
            """);

    [Test]
    public void GenericAllowsRefStruct_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using StructUnion;

            [StructUnion]
            public readonly ref partial struct Box<T> where T : allows ref struct
            {
                public static partial Box<T> Some(T value);
                public static partial Box<T> None();
            }
            """);

    [Test]
    public void UserRefStructWithEqualityOperator_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using System;
            using StructUnion;

            public ref struct Buffer
            {
                public Span<byte> Data;
                public static bool operator ==(Buffer l, Buffer r) => l.Data == r.Data;
                public static bool operator !=(Buffer l, Buffer r) => !(l == r);
            }

            [StructUnion]
            public readonly ref partial struct Holder
            {
                public static partial Holder Buf(Buffer buffer);
                public static partial Holder Number(int value);
            }
            """);

    [Test]
    public void UnmanagedRefStructField_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using StructUnion;

            // All-unmanaged fields, so IsUnmanagedType is true and the size computes — this is the
            // shape that slipped past the managed-value-type check into explicit layout.
            public ref struct Plain
            {
                public int X;
                public static bool operator ==(Plain l, Plain r) => l.X == r.X;
                public static bool operator !=(Plain l, Plain r) => !(l == r);
            }

            [StructUnion]
            public readonly ref partial struct Holder
            {
                public static partial Holder A(Plain p);
                public static partial Holder B(int n);
            }
            """);

    // -- The generated shape --

    [Test]
    public async Task SpanUnion_EmitsRefStructDeclaration()
    {
        var result = GeneratorTestHelper.CreateDriver(SpanUnion).GetRunResult();
        var generated = GeneratedText(result);

        await Assert.That(generated).Contains("readonly ref partial struct Payload");
    }

    [Test]
    public async Task SpanUnion_UsesAutoLayout()
    {
        var result = GeneratorTestHelper.CreateDriver(SpanUnion).GetRunResult();
        var generated = GeneratedText(result);

        // A byref has no representation at a fixed offset.
        await Assert.That(generated).DoesNotContain("LayoutKind.Explicit");
        await Assert.That(generated).DoesNotContain("FieldOffset");
    }

    [Test]
    public async Task UnmanagedRefStructField_UsesAutoLayout()
    {
        var source = """
            using StructUnion;

            public ref struct Plain { public int X; }

            [StructUnion]
            public readonly ref partial struct Holder
            {
                public static partial Holder A(Plain p);
                public static partial Holder B(int n);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();
        var generated = GeneratedText(result);

        await Assert.That(generated).DoesNotContain("LayoutKind.Explicit");
        await Assert.That(generated).DoesNotContain("FieldOffset");
    }

    [Test]
    public async Task NestedAccessors_EmitsRefCaseStruct()
    {
        var source = """
            using System;
            using StructUnion;

            [StructUnion(NestedAccessors = true)]
            public readonly ref partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
                public static partial Payload Number(int value);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();
        var generated = GeneratedText(result);

        await Assert.That(generated).Contains("public readonly ref struct Bytes");
        // A variant carrying no byref stays a plain struct — `ref` is per case, not per union.
        await Assert.That(generated).Contains("public readonly struct Number");
    }

    [Test]
    public async Task SpanUnion_ComparesSpansWithOperator()
    {
        var result = GeneratorTestHelper.CreateDriver(SpanUnion).GetRunResult();
        var generated = GeneratedText(result);

        // EqualityComparer<T>.Default cannot take a ref struct type argument.
        await Assert.That(generated).DoesNotContain("EqualityComparer<global::System.Span<byte>>");
        await Assert.That(generated).Contains("_bytes_data == other._bytes_data");
    }

    [Test]
    public async Task SpanUnion_ExcludesRefLikeFieldsFromHash()
    {
        var result = GeneratorTestHelper.CreateDriver(SpanUnion).GetRunResult();
        var generated = GeneratedText(result);

        // HashCode.Combine is generic, so the span is skipped; the tag still distinguishes variants.
        await Assert.That(generated).Contains("Tags.Bytes => global::System.HashCode.Combine(_tag),");
        await Assert.That(generated).Contains("Tags.Number => global::System.HashCode.Combine(_tag, _number_value),");
    }

    [Test]
    public async Task SpanUnion_EmitsObsoleteEqualsObject()
    {
        var result = GeneratorTestHelper.CreateDriver(SpanUnion).GetRunResult();
        var generated = GeneratedText(result);

        // `obj is Payload` would be CS8121 — a ref struct can never be boxed.
        await Assert.That(generated).DoesNotContain("obj is Payload other");
        await Assert.That(generated).Contains("public override bool Equals(object? obj)");
        await Assert.That(generated).Contains("global::System.Obsolete(");
    }

    [Test]
    public async Task SpanUnion_CallsToStringOnRefLikeFieldInInterpolation()
    {
        var result = GeneratorTestHelper.CreateDriver(SpanUnion).GetRunResult();
        var generated = GeneratedText(result);

        // A bare hole would route through the handler's generic AppendFormatted<T>.
        await Assert.That(generated).Contains("{_bytes_data.ToString()}");
    }

    [Test]
    public async Task GenericAllowsRefStruct_PropagatesConstraint()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly ref partial struct Box<T> where T : allows ref struct
            {
                public static partial Box<T> Some(T value);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();
        var generated = GeneratedText(result);

        await Assert.That(generated).Contains("where T : allows ref struct");
        await Assert.That(generated).Contains("readonly ref partial struct Box<T>");
    }

    // -- Ref safety is preserved through the generated factory --

    [Test]
    public async Task StackallocCannotEscapeThroughFactory()
    {
        var errors = GeneratorTestHelper.GetGeneratedCompilationErrors($$"""
            {{SpanUnion}}

            public static class Escape
            {
                public static Payload Leak()
                {
                    System.Span<byte> local = stackalloc byte[4];
                    return Payload.Bytes(local);
                }
            }
            """);

        // The whole point of a ref struct union: the compiler must still refuse to let a stack
        // buffer outlive its frame, even though the span is laundered through a generated factory
        // that assigns it via Unsafe.AsRef.
        await Assert.That(errors).Contains(d => d.Id is "CS8347" or "CS8352");
    }

    [Test]
    public void SpanFromArrayIsAllowed() =>
        GeneratorTestHelper.AssertGeneratedCompiles($$"""
            {{SpanUnion}}

            public static class NoEscape
            {
                public static int Length(byte[] buffer) => Payload.Bytes(buffer).BytesData.Length;
            }
            """);

    static string GeneratedText(GeneratorDriverRunResult result) =>
        string.Concat(result.GeneratedTrees.Select(t => t.ToString()));
}
