namespace StructUnion.GeneratorTests;

/// <summary>
/// Every union gets an error-level obsolete parameterless constructor unless it opts out with
/// <c>AllowParameterlessConstructor</c>.
/// </summary>
/// <remarks>
/// The opt-out exists for code that must call <c>new T()</c> just to compile, chiefly the
/// System.Text.Json source generator's <c>ObjectCreator</c>. That case runs against the real STJ
/// generator in the integration tests; these cover the flag itself.
/// </remarks>
public class ParameterlessConstructorTests
{
    const string NewShape = """

        public static class Usage
        {
            public static Shape Create() => new Shape();
        }
        """;

    // -- Snapshots --

    [Test]
    public Task Enabled()
    {
        var source = """
            using StructUnion;

            [StructUnion(AllowParameterlessConstructor = true)]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Rectangle(double length, double width);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task DisabledOverridesAssemblyDefault()
    {
        var source = """
            using StructUnion;

            [assembly: StructUnionOptions(AllowParameterlessConstructor = true)]

            [StructUnion(AllowParameterlessConstructor = false)]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Rectangle(double length, double width);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    // -- Cascade --

    [Test]
    public async Task AssemblyDefault_OmitsConstructor()
    {
        var generated = Generate("""
            using StructUnion;

            [assembly: StructUnionOptions(AllowParameterlessConstructor = true)]

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
            }
            """);

        await Assert.That(generated).DoesNotContain("public Shape()");
    }

    // -- `new T()` compiles only when allowed --

    [Test]
    public async Task Default_NewIsAnError()
    {
        var errors = GeneratorTestHelper.GetGeneratedCompilationErrors($$"""
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
            }
            {{NewShape}}
            """);

        await Assert.That(errors).Contains(d => d.Id == "CS0619");
    }

    [Test]
    public void Enabled_NewCompiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles($$"""
            using StructUnion;

            [StructUnion(AllowParameterlessConstructor = true)]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
            }
            {{NewShape}}
            """);

    [Test]
    public void RecordTemplate_Enabled_NewCompiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles($$"""
            using StructUnion;

            [StructUnion(AllowParameterlessConstructor = true)]
            public partial record ShapeRecord(string Label)
            {
                public record Circle(double Radius);
            }
            {{NewShape}}
            """);

    [Test]
    public void RefStruct_Enabled_NewCompiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles($$"""
            using StructUnion;

            [StructUnion(AllowParameterlessConstructor = true)]
            public readonly ref partial struct Shape
            {
                public static partial Shape Bytes(System.Span<byte> data);
            }

            public static class Usage
            {
                public static bool IsDefault() => new Shape().IsDefault;
            }
            """);

    static string Generate(string source) =>
        string.Concat(GeneratorTestHelper.CreateDriver(source).GetRunResult().GeneratedTrees.Select(t => t.ToString()));
}
