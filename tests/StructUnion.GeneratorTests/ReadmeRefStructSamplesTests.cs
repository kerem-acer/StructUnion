namespace StructUnion.GeneratorTests;

/// <summary>
/// The README's ref-struct samples, verified rather than asserted.
/// </summary>
public class ReadmeRefStructSamplesTests
{
    [Test]
    public void PayloadSample_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using System;
            using StructUnion;

            [StructUnion]
            public readonly ref partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
                public static partial Payload Text(ReadOnlySpan<char> text);
                public static partial Payload Number(int value);
            }

            public static class Sample
            {
                public static int Use()
                {
                    Span<byte> buffer = stackalloc byte[3];
                    var p = Payload.Bytes(buffer);
                    return p.Match(
                        bytes: b => b.Length,
                        text: t => t.Length,
                        number: n => n);
                }
            }
            """);

    [Test]
    public async Task LeakSample_ReportsCS8347()
    {
        var errors = GeneratorTestHelper.GetGeneratedCompilationErrors("""
            using System;
            using StructUnion;

            [StructUnion]
            public readonly ref partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
                public static partial Payload Number(int value);
            }

            public static class Sample
            {
                static Payload Leak()
                {
                    Span<byte> local = stackalloc byte[4];
                    return Payload.Bytes(local);
                }
            }
            """);

        await Assert.That(errors).Contains(d => d.Id == "CS8347");
    }

    [Test]
    public void UserWrittenEqualsSample_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Square(double side);

                public bool Equals(Shape other) => Tag == other.Tag;
            }
            """);

    [Test]
    public void GenericBoxSample_Compiles() =>
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
    public void AssemblyLevelOptionsSample_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles(
            """
            using StructUnion;

            [assembly: StructUnionOptions(
                TemplateSuffix = "Template",
                TagPropertyName = "Kind",
                EnableImplicitConversions = false,
                NestedAccessors = true,
                GenerateDispose = true,
                NativeUnion = true,
                GenerateEquality = false)]

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Square(double side);
            }
            """,
            includeUnionRuntime: true);
}
