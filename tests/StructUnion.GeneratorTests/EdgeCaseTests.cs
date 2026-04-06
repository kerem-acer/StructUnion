namespace StructUnion.GeneratorTests;

public class EdgeCaseTests
{
    [Test]
    public Task NestedInClass()
    {
        var source = """
            using StructUnion;

            public partial class Outer
            {
                [StructUnion]
                public readonly partial struct Inner
                {
                    public static partial Inner A(int x);
                    public static partial Inner B(int y);
                }
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task MixedValueTypes()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Mixed
            {
                public static partial Mixed IntCase(int value);
                public static partial Mixed LongCase(long value);
                public static partial Mixed ByteCase(byte value);
                public static partial Mixed FloatCase(float value);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task InternalAccessibility()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            internal readonly partial struct InternalShape
            {
                public static partial InternalShape Circle(double radius);
                public static partial InternalShape Rect(double w, double h);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task DuplicateSingleParamTypes_NoImplicitConversion()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct NumberUnion
            {
                public static partial NumberUnion Celsius(double value);
                public static partial NumberUnion Fahrenheit(double value);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task ManyParameters_UsesHashCodeBuilder()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct BigVariant
            {
                public static partial BigVariant Many(
                    int a, int b, int c, int d,
                    int e, int f, int g, int h);
                public static partial BigVariant Small(int x);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }
}
