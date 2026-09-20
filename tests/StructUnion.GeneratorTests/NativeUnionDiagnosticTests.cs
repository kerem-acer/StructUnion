using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StructUnion.GeneratorTests;

public class NativeUnionDiagnosticTests
{
    const string UnionMarker = "[global::System.Runtime.CompilerServices.Union]";

    const string ShapeSource = """
        using StructUnion;

        [StructUnion(NativeUnion = true)]
        public readonly partial struct Shape
        {
            public static partial Shape Circle(double radius);
            public static partial Shape Rectangle(double length, double width);
        }
        """;

    static string GeneratedText(GeneratorDriverRunResult result) =>
        string.Concat(result.GeneratedTrees.Select(t => t.ToString()));

    [Test]
    public async Task WithoutUnionRuntime_ReportsSU0014()
    {
        // The realistic case: a library that multi-targets and sets NativeUnion once. On the
        // legs without UnionAttribute this must warn and still generate everything else.
        var result = GeneratorTestHelper.CreateDriver(ShapeSource).GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0014");
        await Assert.That(result.Diagnostics).Contains(d => d.Severity == DiagnosticSeverity.Warning);
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(GeneratedText(result)).DoesNotContain(UnionMarker);
    }

    [Test]
    public async Task OldLanguageVersion_ReportsSU0015()
    {
        var result = GeneratorTestHelper
            .CreateDriver(ShapeSource, includeUnionRuntime: true, languageVersion: LanguageVersion.CSharp13)
            .GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0015");
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(GeneratedText(result)).DoesNotContain(UnionMarker);
    }

    [Test]
    public async Task UnionRuntimeAndPreview_ReportsNothing()
    {
        var result = GeneratorTestHelper.CreateDriver(ShapeSource, includeUnionRuntime: true).GetRunResult();

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(GeneratedText(result)).Contains(UnionMarker);
    }

    [Test]
    public async Task KnobOff_NoNativeUnionDiagnostics()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(GeneratedText(result)).DoesNotContain(UnionMarker);
    }

    [Test]
    public async Task TemplateWithCommonFields_ReportsSU0016()
    {
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public partial record ShapeRecord(int Id)
            {
                public record Circle(double Radius);
                public record Rectangle(double Length, double Width);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true).GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0016");

        // Reported, but generation continues without the union members — a single-knob mistake
        // should not cascade into CS0103 all over the user's code.
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(GeneratedText(result)).DoesNotContain(UnionMarker);
    }

    [Test]
    public async Task VariantNamedCases_ReportsSU0011()
    {
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly partial struct Shape
            {
                public static partial Shape Cases(double radius);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true).GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0011");
    }

    [Test]
    public async Task VariantNamedIUnionMembers_ReportsSU0011()
    {
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly partial struct Shape
            {
                public static partial Shape IUnionMembers(double radius);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true).GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0011");
    }

    [Test]
    public async Task VariantNamedIUnionMembers_WithoutNativeUnion_IsFine()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape IUnionMembers(double radius);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task CommonFieldNamedCases_WithNestedAccessors_ReportsSU0017()
    {
        // Pre-existing hole: this used to emit uncompilable CS0102 output with no diagnostic.
        var source = """
            using StructUnion;

            [StructUnion(NestedAccessors = true)]
            public partial record ShapeRecord(int Cases)
            {
                public record Circle(double Radius);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0017");
    }

    [Test]
    public async Task CommonFieldNamedCases_WithoutNestedAccessors_IsFine()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public partial record ShapeRecord(int Cases)
            {
                public record Circle(double Radius);
            }
            """;

        var result = GeneratorTestHelper.CreateDriver(source).GetRunResult();

        await Assert.That(result.Diagnostics).IsEmpty();
    }
}
