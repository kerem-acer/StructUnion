namespace StructUnion.GeneratorTests;

/// <summary>
/// Case parameters whose type carries a nullable reference annotation (<c>string?</c>). Every
/// generated member that names the type must keep the <c>?</c>: the partial implementation, or it
/// no longer matches its declaration (CS8611), and the accessors and delegates, or callers read a
/// value that can be null as one that never is.
/// </summary>
public class NullableAnnotationTests
{
    [Test]
    public Task NullableReferenceParameters()
    {
        var source = """
            #nullable enable
            using StructUnion;

            [StructUnion]
            public readonly partial struct StepMock
            {
                public static partial StepMock Response(int statusCode, string body, string? rawBody);
                public static partial StepMock Note(string? text);
                public static partial StepMock Retry(int? delay);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public async Task PartialMethods_NoWarnings()
    {
        var warnings = GeneratorTestHelper.GetGeneratedCompilationWarnings("""
            #nullable enable
            using StructUnion;

            public interface IValue { }

            [StructUnion(EnableImplicitConversions = false)]
            public readonly partial struct StepMock
            {
                public static partial StepMock Response(int statusCode, IValue body, string? rawBody);
                public static partial StepMock Output(IValue value);
            }
            """);

        await Assert.That(warnings).IsEmpty();
    }

    [Test]
    public async Task NestedAnnotations_NoWarnings()
    {
        var warnings = GeneratorTestHelper.GetGeneratedCompilationWarnings("""
            #nullable enable
            using System.Collections.Generic;
            using StructUnion;

            [StructUnion]
            public readonly partial struct Batch
            {
                public static partial Batch Items(List<string?> items, string?[] names);
                public static partial Batch Lookup(Dictionary<string, object?>? map);
            }
            """);

        await Assert.That(warnings).IsEmpty();
    }

    [Test]
    public async Task NestedAccessors_NoWarnings()
    {
        var warnings = GeneratorTestHelper.GetGeneratedCompilationWarnings("""
            #nullable enable
            using StructUnion;

            [StructUnion(NestedAccessors = true)]
            public readonly partial struct StepMock
            {
                public static partial StepMock Response(int statusCode, string? rawBody);
                public static partial StepMock Empty();
            }
            """);

        await Assert.That(warnings).IsEmpty();
    }

    [Test]
    public async Task NativeUnion_NoWarnings()
    {
        var warnings = GeneratorTestHelper.GetGeneratedCompilationWarnings("""
            #nullable enable
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly partial struct StepMock
            {
                public static partial StepMock Response(int statusCode, string? rawBody);
                public static partial StepMock Empty();
            }
            """,
            includeUnionRuntime: true);

        await Assert.That(warnings).IsEmpty();
    }

    [Test]
    public async Task RecordTemplate_KeepsAnnotations()
    {
        // A template's factories are not partial, so a dropped `?` raises no CS8611 here. It shows
        // only in what callers are told: each dereference below must warn, and nothing else may.
        var warnings = GeneratorTestHelper.GetGeneratedCompilationWarnings("""
            #nullable enable
            using StructUnion;

            [StructUnion]
            public partial record StepMockRecord(string? CorrelationId)
            {
                public record Response(int StatusCode, string? RawBody);
                public record Output(string Value);
            }

            public static class Caller
            {
                public static int Length(StepMock mock) =>
                    mock.CorrelationId.Length + mock.ResponseRawBody.Length + mock.OutputValue.Length;
            }
            """);

        await Assert.That(warnings.Select(d => d.Id)).IsEquivalentTo(["CS8602", "CS8602"]);
    }

    [Test]
    public async Task Disposable_NoWarnings()
    {
        var warnings = GeneratorTestHelper.GetGeneratedCompilationWarnings("""
            #nullable enable
            using System.IO;
            using StructUnion;

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource File(Stream? stream);
                public static partial Resource Pair(Stream? input, Stream output);
            }
            """);

        await Assert.That(warnings).IsEmpty();
    }

    [Test]
    public async Task Accessor_ReadsAsMaybeNull()
    {
        // The accessor is only honest if dereferencing it without a check warns.
        var warnings = GeneratorTestHelper.GetGeneratedCompilationWarnings("""
            #nullable enable
            using StructUnion;

            [StructUnion]
            public readonly partial struct StepMock
            {
                public static partial StepMock Response(int statusCode, string? rawBody);
                public static partial StepMock Empty();
            }

            public static class Caller
            {
                public static int Length(StepMock mock) => mock.ResponseRawBody.Length;
            }
            """);

        await Assert.That(warnings).Contains(d => d.Id == "CS8602");
    }

    [Test]
    public void SameTypeWithAndWithoutAnnotation_NoImplicitConversion() =>
        // `string` and `string?` are one type to the compiler, so both cases claiming a conversion
        // from it would declare the same operator twice.
        GeneratorTestHelper.AssertGeneratedCompiles("""
            #nullable enable
            using StructUnion;

            [StructUnion]
            public readonly partial struct Message
            {
                public static partial Message Text(string value);
                public static partial Message Note(string? value);
            }
            """);
}
