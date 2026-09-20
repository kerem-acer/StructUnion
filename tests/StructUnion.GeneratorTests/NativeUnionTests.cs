namespace StructUnion.GeneratorTests;

public class NativeUnionTests
{
    [Test]
    public Task BasicNativeUnion()
    {
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Rectangle(double length, double width);
                public static partial Shape Empty();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true);
        return Verify(driver);
    }

    [Test]
    public Task WithNestedAccessors()
    {
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true, NestedAccessors = true)]
            public readonly partial struct DrawCmd
            {
                public static partial DrawCmd MoveTo(double x, double y);
                public static partial DrawCmd Close();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true);
        return Verify(driver);
    }

    [Test]
    public Task GenericUnion()
    {
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly partial struct Option<T>
            {
                public static partial Option<T> Some(T value);
                public static partial Option<T> None();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true);
        return Verify(driver);
    }

    [Test]
    public Task DuplicatePayloadTypes()
    {
        // Two variants sharing a payload type are still two distinct case types — the reason case
        // types are generated per variant rather than taken from the payload.
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Square(double side);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true);
        return Verify(driver);
    }

    [Test]
    public Task WithImplicitConversions()
    {
        // The user-defined operator from the payload type must coexist with the language-provided
        // union conversion from the case type.
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly partial struct Payload
            {
                public static partial Payload Number(int value);
                public static partial Payload Text(string value);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true);
        return Verify(driver);
    }

    [Test]
    public Task RecordTemplateWithoutCommonFields()
    {
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public partial record ShapeRecord
            {
                public record Circle(double Radius);
                public record Rectangle(double Length, double Width);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true);
        return Verify(driver);
    }

    [Test]
    public Task NestedInClass()
    {
        // Pins that the provider interface and Create bodies qualify the union correctly when it
        // is not a top-level type.
        var source = """
            using StructUnion;

            namespace MyApp
            {
                public partial class Outer
                {
                    [StructUnion(NativeUnion = true)]
                    public readonly partial struct Inner
                    {
                        public static partial Inner Value(int x);
                    }
                }
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true);
        return Verify(driver);
    }

    [Test]
    public Task ZeroParameterVariantsOnly()
    {
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly partial struct Signal
            {
                public static partial Signal Start();
                public static partial Signal Stop();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true);
        return Verify(driver);
    }

    [Test]
    public Task AssemblyLevelDefault()
    {
        var source = """
            using StructUnion;

            [assembly: StructUnionOptions(NativeUnion = true)]

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true);
        return Verify(driver);
    }

    [Test]
    public Task WithGenerateDispose()
    {
        var source = """
            using StructUnion;

            [StructUnion(NativeUnion = true, GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource File(System.IO.FileStream stream);
                public static partial Resource Inline(int value);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source, includeUnionRuntime: true);
        return Verify(driver);
    }
}
