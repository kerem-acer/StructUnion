using Microsoft.CodeAnalysis;

namespace StructUnion.GeneratorTests;

public class DiagnosticTests
{
    [Test]
    public async Task NonPartialStruct_ReportsSU0001()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly struct Shape
            {
                public static Shape Circle(double radius) => default;
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0001");
    }

    [Test]
    public async Task NonReadonlyStruct_ReportsSU0002()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public partial struct Shape
            {
                public static partial Shape Circle(double radius);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0002");
    }

    [Test]
    public async Task NoVariantMethods_ReportsSU0003()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public int GetValue() => 42;
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0003");
    }

    [Test]
    public async Task MethodReturnsWrongType_ReportsSU0004()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial int NotAVariant(double radius);
                public static partial Shape Circle(double radius);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        // Circle is still valid, so code should be generated
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0004");
    }

    [Test]
    public async Task RefParameter_ReportsSU0005()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(ref double radius);
                public static partial Shape Rectangle(double w, double h);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        // Rectangle is still valid, so code should be generated
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0005");
    }

    [Test]
    public async Task InParameter_ReportsSU0005()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(in double radius);
                public static partial Shape Rectangle(double w, double h);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0005");
    }

    [Test]
    public async Task OutParameter_ReportsSU0005()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(out double radius);
                public static partial Shape Rectangle(double w, double h);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0005");
    }

    [Test]
    public async Task LargeStruct_ReportsSU0007()
    {
        // 9 doubles = 72 bytes > 64 byte threshold
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct BigUnion
            {
                public static partial BigUnion A(
                    double f1, double f2, double f3,
                    double f4, double f5, double f6,
                    double f7, double f8, double f9);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        // Should still generate code (it's a warning, not error)
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(result.Diagnostics).Contains(d =>
            d.Id == "SU0007" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task SmallStruct_DoesNotReportSU0007()
    {
        // 2 doubles = 16 bytes, well under threshold
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

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0007");
    }

    [Test]
    public async Task NoVariantsInTemplate_ReportsSU0003()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public record ShapeTemplate
            {
                // No nested types = no variants
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0003");
    }

    [Test]
    public async Task TooManyVariants_ReportsSU0006()
    {
        // Generate 256 static partial methods to exceed the 255 max
        var methods = string.Join("\n",
            Enumerable.Range(0, 256)
                .Select(i => $"        public static partial TooMany V{i}(int x);"));

        var source = $$"""
            using StructUnion;

            [StructUnion]
            public readonly partial struct TooMany
            {
            {{methods}}
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0006");
    }

    [Test]
    public async Task TemplateWithPrimaryConstructor_GeneratesWithCommonFields()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public record ResultTemplate(System.Guid Id)
            {
                public record Ok(string Value);
                public record Error(string Message);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(result.Diagnostics).DoesNotContain(d =>
            d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task RecordSuffixSetting_IsRespected()
    {
        var source = """
            using StructUnion;

            [assembly: StructUnionOptions(TemplateSuffix = "Union")]

            [StructUnion]
            public record ShapeUnion
            {
                public record Circle(double Radius);
                public record Rectangle(double Width, double Height);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        // "ShapeUnion" minus "Union" suffix = struct named "Shape"
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task GenericWithInterfaceConstraint_Generates()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Wrapper<T> where T : System.IDisposable
            {
                public static partial Wrapper<T> Some(T value);
                public static partial Wrapper<T> None();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
    }

    // ── Layout XML doc in generated code ──

    [Test]
    public async Task ExplicitLayout_GeneratesXmlDocWithLayout()
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

        var generatedSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .First(s => s.Contains("partial struct Shape"));

        await Assert.That(generatedSource).Contains("/// <remarks>");
        await Assert.That(generatedSource).Contains("bytes");
        await Assert.That(generatedSource).Contains("Circle");
        await Assert.That(generatedSource).Contains("Rectangle");
    }

    [Test]
    public async Task TemplateRecord_GeneratesXmlDocOnTemplate()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public partial record ShapeRecord
            {
                public record Circle(double Radius);
                public record Rectangle(double Width, double Height);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        var templateDoc = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("partial record ShapeRecord"));

        await Assert.That(templateDoc).IsNotNull();
        await Assert.That(templateDoc).Contains("/// <remarks>");
        await Assert.That(templateDoc).Contains("bytes");
    }

    [Test]
    public async Task AutoLayout_DoesNotGenerateLayoutDoc()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Option<T>
            {
                public static partial Option<T> Some(T value);
                public static partial Option<T> None();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        var generatedSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .First(s => s.Contains("partial struct Option"));

        await Assert.That(generatedSource).DoesNotContain("/// <remarks>");
    }

    [Test]
    public async Task CaseInsensitiveVariantNameCollision_ReportsSU0008()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Result
            {
                public static partial Result Ok(int value);
                public static partial Result OK(string message);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0008");
    }

    [Test]
    public async Task CaseInsensitiveVariantNameCollision_Template_ReportsSU0008()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public record ResultRecord
            {
                public record Ok(int Value);
                public record OK(string Message);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0008");
    }

    [Test]
    public async Task VariantNamedTag_ReportsSU0009()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Event
            {
                public static partial Event Tag(string value);
                public static partial Event Click(int x);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0009");
    }

    [Test]
    public async Task CommonFieldNamedTag_ReportsSU0009()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public partial record EventRecord(int Tag)
            {
                public record Click(int X);
                public record KeyPress(char Key);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0009");
    }

    [Test]
    public async Task CustomTagPropertyName_AvoidsSU0009()
    {
        var source = """
            using StructUnion;

            [StructUnion(TagPropertyName = "Kind")]
            public readonly partial struct Event
            {
                public static partial Event Tag(string value);
                public static partial Event Click(int x);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0009");
    }

    // ── SU0010: GeneratedName + Suffix conflict ──

    [Test]
    public async Task GeneratedNameAndSuffix_ReportsSU0010()
    {
        var source = """
            using StructUnion;

            [StructUnion("MyShape", TemplateSuffix = "Def")]
            public record ShapeDef
            {
                public record Circle(double Radius);
                public record Rectangle(double Width, double Height);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0010");
    }

    // ── Options cascade ──

    [Test]
    public async Task AssemblyLevelTagPropertyName_Cascades()
    {
        var source = """
            using StructUnion;

            [assembly: StructUnionOptions(TagPropertyName = "Kind")]

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Rectangle(double w, double h);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);

        var generatedSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .First(s => s.Contains("partial struct Shape"));

        await Assert.That(generatedSource).Contains("Tags Kind =>");
    }

    [Test]
    public async Task PerTypeTagPropertyName_OverridesAssembly()
    {
        var source = """
            using StructUnion;

            [assembly: StructUnionOptions(TagPropertyName = "Kind")]

            [StructUnion(TagPropertyName = "Variant")]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Rectangle(double w, double h);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);

        var generatedSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .First(s => s.Contains("partial struct Shape"));

        await Assert.That(generatedSource).Contains("Tags Variant =>");
        await Assert.That(generatedSource).DoesNotContain("Tags Kind =>");
    }

    [Test]
    public async Task SuffixOverridesAssemblyDefault()
    {
        var source = """
            using StructUnion;

            [assembly: StructUnionOptions(TemplateSuffix = "Record")]

            [StructUnion(TemplateSuffix = "Def")]
            public record ShapeDef
            {
                public record Circle(double Radius);
                public record Rectangle(double Width, double Height);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        // "ShapeDef" minus "Def" suffix = struct named "Shape"
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);

        var generatedSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("partial struct Shape"));

        await Assert.That(generatedSource).IsNotNull();
    }

    [Test]
    public async Task EmptySuffix_NoTrimming()
    {
        var source = """
            using StructUnion;

            [StructUnion(TemplateSuffix = "")]
            public record ShapeRecord
            {
                public record Circle(double Radius);
                public record Rectangle(double Width, double Height);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);

        // With empty suffix, no trimming — struct is named "ShapeRecord"
        var generatedSource = result.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .FirstOrDefault(s => s.Contains("partial struct ShapeRecord"));

        await Assert.That(generatedSource).IsNotNull();
    }

    // ── SU0011: Reserved variant name ──

    [Test]
    public async Task VariantNamedDefault_ReportsSU0011()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct MyUnion
            {
                public static partial MyUnion Default(int value);
                public static partial MyUnion Other(int x);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0011");
    }

    [Test]
    public async Task VariantNamedTags_ReportsSU0011()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct MyUnion
            {
                public static partial MyUnion Tags(string label);
                public static partial MyUnion Other(int x);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0011");
    }

    [Test]
    public async Task TemplateVariantNamedDefault_ReportsSU0011()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public record MyUnionRecord
            {
                public record Default(int Value);
                public record Other(int X);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0011");
    }

    [Test]
    public async Task VariantNamedCases_WithNestedAccessors_ReportsSU0011()
    {
        var source = """
            using StructUnion;

            [StructUnion(NestedAccessors = true)]
            public readonly partial struct MyUnion
            {
                public static partial MyUnion Cases(int value);
                public static partial MyUnion Other(int x);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(result.Diagnostics).Contains(d => d.Id == "SU0011");
    }

    [Test]
    public async Task VariantNamedCases_WithoutNestedAccessors_DoesNotReportSU0011()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct MyUnion
            {
                public static partial MyUnion Cases(int value);
                public static partial MyUnion Other(int x);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0011");
    }

    // ── Boundary tests ──

    [Test]
    public async Task MaxVariants_255_DoesNotReportSU0006()
    {
        var methods = string.Join("\n",
            Enumerable.Range(0, 255)
                .Select(i => $"        public static partial MaxUnion V{i}(int x);"));

        var source = $$"""
            using StructUnion;

            [StructUnion]
            public readonly partial struct MaxUnion
            {
            {{methods}}
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0006");
    }

    [Test]
    public async Task ExactlyAtThreshold_64Bytes_DoesNotReportSU0007()
    {
        // 8 doubles = 64 bytes = exactly at threshold, should NOT warn
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct AtThreshold
            {
                public static partial AtThreshold A(
                    double f1, double f2, double f3, double f4,
                    double f5, double f6, double f7, double f8);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        var result = driver.GetRunResult();

        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
        await Assert.That(result.Diagnostics).DoesNotContain(d => d.Id == "SU0007");
    }
}
