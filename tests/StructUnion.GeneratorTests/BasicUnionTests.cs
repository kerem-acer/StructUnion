namespace StructUnion.GeneratorTests;

public class BasicUnionTests
{
    [Test]
    public Task BasicTwoVariantUnion()
    {
        var source = """
            using StructUnion;

            [StructUnion]
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
    public Task ThreeVariantUnion()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Rectangle(double length, double width);
                public static partial Shape Triangle(double @base, double height);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task EmptyVariant()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Option
            {
                public static partial Option Some(int value);
                public static partial Option None();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task SingleVariant()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Wrapper
            {
                public static partial Wrapper Value(int x);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task WithNamespace()
    {
        var source = """
            using StructUnion;

            namespace MyApp.Models
            {
                [StructUnion]
                public readonly partial struct Result
                {
                    public static partial Result Ok(int value);
                    public static partial Result Error(int code);
                }
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task DisabledImplicitConversions()
    {
        var source = """
            using StructUnion;

            [StructUnion(EnableImplicitConversions = false)]
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
    public Task KeywordParameterNames()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Triangle(double @base, double height);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }
}
