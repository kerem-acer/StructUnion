using Microsoft.CodeAnalysis;

namespace StructUnion.GeneratorTests;

/// <summary>
/// What may share memory, and how big the result is. A wrong answer here does not show up as a
/// compile error but as a <c>TypeLoadException</c> or a wasted layout at runtime.
/// </summary>
public class LayoutTests
{
    const string Names = """
        public readonly struct Names(string[] items)
        {
            public string[] Items { get; } = items;
        }
        """;

    const string Inner = """
        [StructUnion(EnableImplicitConversions = false)]
        public readonly partial struct Inner
        {
            public static partial Inner A(string a, int position);
            public static partial Inner B(string b);
            public static partial Inner F(string f, string g);
        }
        """;

    // ── Hybrid layout ──

    [Test]
    public Task Hybrid_ReferenceHoldingStructBesideSharedStorage()
    {
        var source = $$"""
            using StructUnion;

            {{Names}}

            [StructUnion(EnableImplicitConversions = false)]
            public readonly partial struct Mixed
            {
                public static partial Mixed A(string a);
                public static partial Mixed B(string b);
                public static partial Mixed E(Names names);
                public static partial Mixed N(long low, int high);
            }
            """;

        return Verify(GeneratorTestHelper.CreateDriver(source));
    }

    [Test]
    public Task NestedUnionFromSameCompilation_IsPlacedByRuntime()
    {
        // Inner's fields are generated in the same run, so Roslyn sees an empty, unmanaged struct.
        var source = $$"""
            using StructUnion;

            {{Inner}}

            [StructUnion(EnableImplicitConversions = false)]
            public readonly partial struct Outer
            {
                public static partial Outer A(string a);
                public static partial Outer Nested(Inner inner);
            }
            """;

        return Verify(GeneratorTestHelper.CreateDriver(source)).IgnoreGeneratedResult(r => r.HintName == "Inner.g.cs");
    }

    [Test]
    public async Task TemplateGeneratedStructAsCase_IsPlacedByRuntime()
    {
        // `Pair` does not exist until the template is generated, so it is an error type here.
        var source = """
            using StructUnion;

            [StructUnion]
            public partial record PairRecord
            {
                public record Ints(int X, int Y);
            }

            [StructUnion(EnableImplicitConversions = false)]
            public readonly partial struct UsesPair
            {
                public static partial UsesPair Text(string text);
                public static partial UsesPair WithPair(Pair pair);
            }
            """;

        var generated = GeneratedSource(source, "UsesPair.g.cs");

        await Assert.That(generated).Contains("private readonly Pair _withpair_pair;");
        await Assert.That(generated).Contains("internal readonly string _text_text;");
        GeneratorTestHelper.AssertGeneratedCompiles(source);
    }

    [Test]
    public async Task GenericUnionWithKnownSizes_IsSequential()
    {
        // "generic types cannot have explicit layout", even when no field mentions a type parameter.
        var source = """
            using System.Collections.Generic;
            using StructUnion;

            [StructUnion(EnableImplicitConversions = false)]
            public readonly partial struct Box<T>
            {
                public static partial Box<T> Items(List<T> items);
                public static partial Box<T> Text(string text);
            }

            public partial class Container<T>
            {
                [StructUnion]
                public readonly partial struct Nested
                {
                    public static partial Nested Text(string text);
                    public static partial Nested Number(int number);
                }
            }
            """;

        await Assert.That(GeneratedSource(source, "Box.g.cs")).DoesNotContain("LayoutKind.Explicit");
        await Assert.That(GeneratedSource(source, "Container.Nested.g.cs")).DoesNotContain("LayoutKind.Explicit");
    }

    [Test]
    public void Hybrid_WithDispose_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles($$"""
            using System;
            using StructUnion;

            {{Names}}

            public sealed class Handle : IDisposable { public void Dispose() { } }

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource Open(Handle handle);
                public static partial Resource Listed(Names names);
                public static partial Resource Code(int code);
            }
            """);

    [Test]
    public void Hybrid_WithNestedAccessors_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles($$"""
            using StructUnion;

            {{Names}}

            [StructUnion(NestedAccessors = true)]
            public readonly partial struct Cmd
            {
                public static partial Cmd Text(string value, int line);
                public static partial Cmd Listed(Names names, string title);
            }
            """);

    [Test]
    public void Hybrid_NativeUnion_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles($$"""
            using StructUnion;

            {{Names}}

            [StructUnion(NativeUnion = true)]
            public readonly partial struct Payload
            {
                public static partial Payload Text(string value);
                public static partial Payload Listed(Names names);
            }
            """,
            includeUnionRuntime: true);

    [Test]
    public void Hybrid_RecordTemplateWithCommonFields_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using StructUnion;

            [StructUnion]
            public partial record TaggedRecord(string Id, (string Key, int Count) Origin)
            {
                public record Plain(string Text);
                public record Pair((string Name, int Count) Entry, long Stamp);
            }
            """);

    // ── SU0007 follows the generated layout ──

    [Test]
    public async Task SU0007_CountsTagAndPadding()
    {
        // tag + 7 padding + 8 doubles = 72; the payload alone would be exactly 64.
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct EightDoubles
            {
                public static partial EightDoubles A(
                    double f1, double f2, double f3, double f4,
                    double f5, double f6, double f7, double f8);
            }
            """;

        await AssertSU0007(source, 72);
    }

    [Test]
    public async Task SU0007_SequentialUnion_CountsEveryCase()
    {
        // Generic, so sequential: nine strings side by side, though the largest case is 8 bytes.
        var cases = string.Join("\n", Enumerable.Range(0, 9).Select(i => $"    public static partial Many<T> V{i}(string s);"));
        var source = $$"""
            using StructUnion;

            [StructUnion(EnableImplicitConversions = false)]
            public readonly partial struct Many<T>
            {
            {{cases}}
            }
            """;

        await AssertSU0007(source, 80);
    }

    [Test]
    public async Task SU0007_NestedUnionFromSameCompilation_CountsItsSize()
    {
        // Storage: tag + three decimals = 56. Inner, 24 bytes, beside it: 80.
        var source = $$"""
            using StructUnion;

            {{Inner}}

            [StructUnion(EnableImplicitConversions = false)]
            public readonly partial struct Outer
            {
                public static partial Outer Nested(Inner inner);
                public static partial Outer Big(decimal a, decimal b, decimal c);
            }
            """;

        await AssertSU0007(source, 80);
    }

    [Test]
    public async Task SU0007_NestedUnionFromReferencedAssembly_CountsItsSize()
    {
        // Inner's six fields add up to 48 bytes, but its cases overlap in 24.
        var (result, errors) = GeneratorTestHelper.RunWithLibrary(
            """
            using StructUnion;

            [StructUnion(EnableImplicitConversions = false)]
            public readonly partial struct Outer
            {
                public static partial Outer Nested(Inner inner);
                public static partial Outer Big(decimal a, decimal b, decimal c);
            }
            """,
            $$"""
            using StructUnion;

            {{Inner}}
            """);

        await Assert.That(errors).IsEmpty();
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0007" && d.GetMessage().Contains(" 80 bytes"));
    }

    [Test]
    public async Task HybridUnionFromReferencedAssembly_IsPlacedByRuntime()
    {
        var (result, errors) = GeneratorTestHelper.RunWithLibrary(
            """
            using StructUnion;

            [StructUnion(EnableImplicitConversions = false)]
            public readonly partial struct Holder
            {
                public static partial Holder Held(Mixed mixed);
                public static partial Holder Text(string text);
            }
            """,
            $$"""
            using StructUnion;

            {{Names}}

            [StructUnion(EnableImplicitConversions = false)]
            public readonly partial struct Mixed
            {
                public static partial Mixed A(string a);
                public static partial Mixed B(string b);
                public static partial Mixed E(Names names);
            }
            """);

        var generated = result.Results.SelectMany(r => r.GeneratedSources).Single(s => s.HintName == "Holder.g.cs").SourceText.ToString();

        await Assert.That(errors).IsEmpty();
        await Assert.That(generated).Contains("private readonly global::Mixed _held_mixed;");
        // Storage (tag + string) is 16 bytes; Mixed rebuilt from its fields is 24.
        await Assert.That(generated).Contains("<b>~40 bytes</b>");
    }

    static string GeneratedSource(string source, string hintName) =>
        GeneratorTestHelper.CreateDriver(source).GetRunResult().Results
            .SelectMany(r => r.GeneratedSources)
            .Single(s => s.HintName == hintName)
            .SourceText.ToString();

    static async Task AssertSU0007(string source, int expectedSize)
    {
        var diagnostics = GeneratorTestHelper.CreateDriver(source).GetRunResult().Diagnostics;
        await Assert.That(diagnostics).Contains(d =>
            d.Id == "SU0007" && d.GetMessage().Contains($" {expectedSize} bytes"));
    }
}
