using System.Reflection;
using System.Runtime.Loader;

namespace StructUnion.GeneratorTests;

/// <summary>
/// <c>[StructUnion]</c> and <c>[StructUnionOptions]</c> are <c>[Conditional]</c>, so a compiled union
/// assembly does not depend on <c>StructUnion.dll</c> unless <c>STRUCTUNION_KEEP_ATTRIBUTES</c> is defined.
/// </summary>
/// <remarks>
/// Without this, a consumer referencing the package with <c>PrivateAssets="all"</c> ships no
/// <c>StructUnion.dll</c>, and anything that reflects over a union's attributes (System.Text.Json
/// looks for <c>[JsonPolymorphic]</c> on every type it serializes) throws FileNotFoundException.
/// </remarks>
public class ConditionalAttributeTests
{
    const string KeepSymbol = "STRUCTUNION_KEEP_ATTRIBUTES";

    const string Source = """
        using StructUnion;

        [assembly: StructUnionOptions(TagPropertyName = "Kind")]

        [StructUnion]
        public readonly partial struct Shape
        {
            public static partial Shape Circle(double radius);
            public static partial Shape Label(string text);
        }

        [StructUnion]
        public partial record EventRecord
        {
            public record Click(int X, int Y);
        }
        """;

    [Test]
    public async Task Default_AssemblyDoesNotReferenceStructUnion()
    {
        var metadata = Inspect(GeneratorTestHelper.EmitGenerated(Source));

        await Assert.That(metadata.References).DoesNotContain("StructUnion");
        await Assert.That(metadata.AttributesOn["Shape"]).DoesNotContain("StructUnion.StructUnionAttribute");
        await Assert.That(metadata.AttributesOn["EventRecord"]).DoesNotContain("StructUnion.StructUnionAttribute");
        await Assert.That(metadata.AssemblyAttributes).DoesNotContain("StructUnion.StructUnionOptionsAttribute");
    }

    [Test]
    public async Task Default_OptionsStillApply()
    {
        // The generator reads the attributes from source, so dropping them from metadata must not
        // drop them from generation: the assembly option renames the tag property.
        var metadata = Inspect(GeneratorTestHelper.EmitGenerated(Source));

        await Assert.That(metadata.PropertiesOn["Shape"]).Contains("Kind");
        await Assert.That(metadata.PropertiesOn["Event"]).Contains("Kind");
    }

    [Test]
    public async Task KeepSymbol_KeepsAttributes()
    {
        var metadata = Inspect(GeneratorTestHelper.EmitGenerated(Source, KeepSymbol));

        await Assert.That(metadata.References).Contains("StructUnion");
        await Assert.That(metadata.AttributesOn["Shape"]).Contains("StructUnion.StructUnionAttribute");
        await Assert.That(metadata.AttributesOn["EventRecord"]).Contains("StructUnion.StructUnionAttribute");
        await Assert.That(metadata.AssemblyAttributes).Contains("StructUnion.StructUnionOptionsAttribute");
    }

    /// <summary>Loads <paramref name="image"/> into a throwaway context and reads what reached its metadata.</summary>
    static EmittedMetadata Inspect(byte[] image)
    {
        var context = new AssemblyLoadContext(name: null, isCollectible: true);
        try
        {
            using var stream = new MemoryStream(image);
            var assembly = context.LoadFromStream(stream);
            var types = assembly.GetTypes();

            return new EmittedMetadata(
                assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray(),
                assembly.GetCustomAttributesData().Select(a => a.AttributeType.FullName!).ToArray(),
                types.ToDictionary(t => t.FullName!, t => t.GetCustomAttributesData().Select(a => a.AttributeType.FullName!).ToArray()),
                types.ToDictionary(t => t.FullName!, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToArray()));
        }
        finally
        {
            context.Unload();
        }
    }

    sealed record EmittedMetadata(
        string[] References,
        string[] AssemblyAttributes,
        Dictionary<string, string[]> AttributesOn,
        Dictionary<string, string[]> PropertiesOn);
}
