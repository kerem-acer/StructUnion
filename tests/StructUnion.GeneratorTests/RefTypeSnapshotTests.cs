namespace StructUnion.GeneratorTests;

public class RefTypeSnapshotTests
{
    [Test]
    public Task MixedRefAndValueTypes()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public record PayloadRecord
            {
                public record Text(string Value);
                public record Number(int Value);
                public record Both(string Name, int Age);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }

    [Test]
    public Task AllRefTypes()
    {
        var source = """
            using StructUnion;

            [StructUnion]
            public record MessageRecord
            {
                public record Text(string Value);
                public record Data(int[] Items);
                public record Error(System.Exception Ex);
            }
            """;

        var driver = GeneratorTestHelper.CreateDriver(source);
        return Verify(driver);
    }
}
