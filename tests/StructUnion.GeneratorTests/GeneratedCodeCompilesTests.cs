namespace StructUnion.GeneratorTests;

/// <summary>
/// Proves the generated output actually compiles. The snapshot tests only diff text, so without
/// these a union whose generated code is syntactically fine but semantically broken passes.
/// </summary>
public class GeneratedCodeCompilesTests
{
    [Test]
    public void BasicUnion_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using StructUnion;

            [StructUnion]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Rectangle(double length, double width);
                public static partial Shape Empty();
            }
            """);

    [Test]
    public void MixedRefAndValueTypes_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using StructUnion;

            [StructUnion]
            public readonly partial struct Payload
            {
                public static partial Payload Text(string value);
                public static partial Payload Number(int value);
                public static partial Payload Both(string name, int age);
            }
            """);

    [Test]
    public void GenericUnion_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using StructUnion;

            [StructUnion]
            public readonly partial struct Result<T, TError>
            {
                public static partial Result<T, TError> Ok(T value);
                public static partial Result<T, TError> Error(TError error);
            }
            """);

    [Test]
    public void NestedAccessors_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using StructUnion;

            [StructUnion(NestedAccessors = true)]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Rectangle(double length, double width);
            }
            """);

    [Test]
    public void RecordTemplate_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using StructUnion;

            [StructUnion]
            public partial record ShapeRecord(int Id)
            {
                public record Circle(double Radius);
                public record Rectangle(double Length, double Width);
            }
            """);

    [Test]
    public void ManagedValueTypeField_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using StructUnion;

            [StructUnion]
            public readonly partial struct Mixed
            {
                public static partial Mixed Pair((string Name, int Age) value);
                public static partial Mixed Number(int value);
            }
            """);

    [Test]
    public void DisposableUnion_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles("""
            using System.IO;
            using StructUnion;

            [StructUnion(GenerateDispose = true)]
            public readonly partial struct Resource
            {
                public static partial Resource File(FileStream stream);
                public static partial Resource Number(int value);
            }
            """);

    [Test]
    public void NativeUnion_Compiles() =>
        GeneratorTestHelper.AssertGeneratedCompiles(
            """
            using StructUnion;

            [StructUnion(NativeUnion = true)]
            public readonly partial struct Shape
            {
                public static partial Shape Circle(double radius);
                public static partial Shape Rectangle(double length, double width);
                public static partial Shape Empty();
            }
            """,
            includeUnionRuntime: true);
}
