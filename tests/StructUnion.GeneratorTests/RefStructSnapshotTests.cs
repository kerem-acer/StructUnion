namespace StructUnion.GeneratorTests;

public class RefStructSnapshotTests
{
    [Test]
    public Task SpanUnion()
    {
        var source = """
            using System;
            using StructUnion;

            [StructUnion]
            public readonly ref partial struct Payload
            {
                public static partial Payload Bytes(Span<byte> data);
                public static partial Payload Number(int value);
                public static partial Payload Empty();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task SpanUnionWithNestedAccessors()
    {
        var source = """
            using System;
            using StructUnion;

            [StructUnion(NestedAccessors = true)]
            public readonly ref partial struct Token
            {
                public static partial Token Text(ReadOnlySpan<char> text);
                public static partial Token Number(int value);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task GenericAllowsRefStruct()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly ref partial struct Box<T> where T : allows ref struct
            {
                public static partial Box<T> Some(T value);
                public static partial Box<T> None();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task NonComparableRefStructOmitsEquality()
    {
        var source = """
            using System;
            using StructUnion;

            public ref struct Buffer { public Span<byte> Data; }

            [StructUnion]
            public readonly ref partial struct Holder
            {
                public static partial Holder Buf(Buffer buffer);
                public static partial Holder Number(int value);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task EqualityDisabled()
    {
        var source = """
            using StructUnion;

            [StructUnion(GenerateEquality = false)]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Square(double side);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task UserWrittenEqualsSelf()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Square(double side);

                public bool Equals(Shape other) => _tag == other._tag;
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }
}
