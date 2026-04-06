namespace StructUnion.GeneratorTests;

public class GenericUnionTests
{
    [Test]
    public Task GenericOptionType()
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
        return Verify(driver);
    }

    [Test]
    public Task GenericResultType()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Result<TOk, TError>
            {
                public static partial Result<TOk, TError> Ok(TOk value);
                public static partial Result<TOk, TError> Error(TError error);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task GenericWithStructConstraint()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Option<T> where T : struct
            {
                public static partial Option<T> Some(T value);
                public static partial Option<T> None();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task GenericWithUnmanagedConstraint()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public readonly partial struct Option<T> where T : unmanaged
            {
                public static partial Option<T> Some(T value);
                public static partial Option<T> None();
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }
}
