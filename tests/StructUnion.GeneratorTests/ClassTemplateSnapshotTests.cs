namespace StructUnion.GeneratorTests;

public class ClassTemplateSnapshotTests
{
    [Test]
    public Task ClassTemplate_BasicWithProperties()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public class ShapeRecord
            {
                public int Id { get; }

                public class Circle
                {
                    public double Radius { get; }
                }

                public class Rectangle
                {
                    public double Length { get; }
                    public double Width { get; }
                }
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task ClassTemplate_NoCommonFields()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public class ResultRecord
            {
                public class Ok
                {
                    public int Value { get; }
                }

                public class Error
                {
                    public int Code { get; }
                    public string Message { get; }
                }
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task ClassTemplate_ExplicitName()
    {
        var source = """
            using StructUnion;

            [StructUnion("MyShape")]
            public class ShapeDef
            {
                public class Circle
                {
                    public double Radius { get; }
                }

                public class Square
                {
                    public double Side { get; }
                }
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task RecordTemplate_WithProperties()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public record ShapeRecord
            {
                public int Id { get; init; }

                public record Circle(double Radius);
                public record Rectangle(double Length, double Width);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

}
