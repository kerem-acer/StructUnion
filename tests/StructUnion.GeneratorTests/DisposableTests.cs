using Microsoft.CodeAnalysis;

namespace StructUnion.GeneratorTests;

public class DisposableTests
{
    [Test]
    public Task SingleDisposableVariant_GeneratesDisposeAndTake()
    {
        var source = """
            using StructUnion;

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource File(System.IO.MemoryStream stream);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task MixedDisposableAndPlain_OnlyDisposesActiveVariant()
    {
        var source = """
            using StructUnion;

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource File(System.IO.MemoryStream stream);
                public static partial Resource Buffer(System.Buffers.IMemoryOwner<byte> owner);
                public static partial Resource Inline(int value);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task MultiFieldVariant_TakeUsesOutParams()
    {
        var source = """
            using StructUnion;

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource Tagged(System.IO.MemoryStream stream, string label);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task AsyncDisposable_GeneratesDisposeAsync()
    {
        // FileStream implements both IDisposable and IAsyncDisposable on net6+.
        var source = """
            using StructUnion;

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource File(System.IO.FileStream stream);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task ConstrainedGeneric_TreatedAsDisposable()
    {
        var source = """
            using StructUnion;

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Box<T> where T : System.IDisposable
            {
                public static partial Box<T> Some(T value);
                public static partial Box<T> None();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task UnconstrainedGeneric_NotTreatedAsDisposable()
    {
        // No constraint => statically unknown => no dispose, no Take, no SU0013.
        var source = """
            using StructUnion;

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Box<T>
            {
                public static partial Box<T> Some(T value);
                public static partial Box<T> None();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task RecordTemplate_DisposableVariant_Generates()
    {
        var source = """
            using StructUnion;

            [StructUnion(GenerateDispose = true)]
            public partial record ResourceRecord
            {
                public record File(System.IO.MemoryStream Stream);
                public record Inline(int Value);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    // ── Diagnostic SU0013 ──

    [Test]
    public async Task DisposableFieldWithoutOptIn_ReportsSU0013()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Resource
            {
                public static partial Resource File(System.IO.MemoryStream stream);
                public static partial Resource Inline(int value);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d =>
            d.Id == "SU0013" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task DisposableField_WithOptIn_DoesNotReportSU0013()
    {
        var source = """
            using StructUnion;

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource File(System.IO.MemoryStream stream);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0013");
    }

    [Test]
    public async Task NoDisposableField_DoesNotReportSU0013()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Rectangle(double w, double h);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0013");
    }

    [Test]
    public async Task UnconstrainedGeneric_DoesNotReportSU0013()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Box<T>
            {
                public static partial Box<T> Some(T value);
                public static partial Box<T> None();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0013");
    }

    // ── Cascade tests ──

    [Test]
    public async Task AssemblyLevelGenerateDispose_Cascades()
    {
        var source = """
            using StructUnion;

            [assembly: StructUnionOptions(GenerateDispose = true)]

            [StructUnion]
            public readonly partial struct Resource
            {
                public static partial Resource File(System.IO.MemoryStream stream);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0013");

        var generatedSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .First(s => s.Contains("partial struct Resource"));

        await Assert.That(generatedSource).Contains("public void Dispose()");
        await Assert.That(generatedSource).Contains("global::System.IDisposable");
    }

    [Test]
    public async Task PerTypeGenerateDispose_OverridesAssemblyOff()
    {
        var source = """
            using StructUnion;

            [assembly: StructUnionOptions(GenerateDispose = false)]

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource File(System.IO.MemoryStream stream);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        var generatedSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .First(s => s.Contains("partial struct Resource"));

        await Assert.That(generatedSource).Contains("public void Dispose()");
    }

    [Test]
    public async Task GenerateDisposeFalse_DoesNotEmitDispose()
    {
        var source = """
            using StructUnion;

            [StructUnion(GenerateDispose = false)]
            public readonly partial struct Resource
            {
                public static partial Resource Inline(int value);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        var generatedSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .First(s => s.Contains("partial struct Resource"));

        await Assert.That(generatedSource).DoesNotContain("public void Dispose()");
        await Assert.That(generatedSource).DoesNotContain("global::System.IDisposable");
    }
}
