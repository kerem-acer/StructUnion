using System.Text.Json;
using System.Text.Json.Serialization;

namespace StructUnion.IntegrationTests.JsonSourceGeneration;

// The System.Text.Json source generator emits `ObjectCreator = () => new SchemaOrRef()` for any union
// reachable from a [JsonSerializable] type, so this file only compiles because the obsolete
// parameterless constructor is not generated. Do not put [JsonConverter] on the union: that tells the
// generator about the converter at compile time, it skips the object metadata, and the CS0619 this
// guards against can no longer happen.
[StructUnion(AllowParameterlessConstructor = true)]
public readonly partial struct SchemaOrRef
{
    public static partial SchemaOrRef Inline(string schema);
    public static partial SchemaOrRef Ref(string artifact);
}

public sealed record Product
{
    public SchemaOrRef? Credentials { get; init; }
}

[JsonSerializable(typeof(Product))]
internal sealed partial class ModelJsonContext : JsonSerializerContext;

/// <summary>Writes <c>Inline</c> as a JSON string and <c>Ref</c> as <c>{"$ref": "..."}</c>.</summary>
internal sealed class SchemaOrRefConverter : JsonConverter<SchemaOrRef>
{
    public override SchemaOrRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return SchemaOrRef.Inline(reader.GetString()!);
        }

        reader.Read(); // "$ref"
        reader.Read();
        var artifact = reader.GetString()!;
        reader.Read(); // }
        return SchemaOrRef.Ref(artifact);
    }

    public override void Write(Utf8JsonWriter writer, SchemaOrRef value, JsonSerializerOptions options)
    {
        if (value.TryGetInline(out var schema))
        {
            writer.WriteStringValue(schema);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("$ref", value.RefArtifact);
        writer.WriteEndObject();
    }
}

public class JsonSourceGenerationTests
{
    static ModelJsonContext CreateContext() =>
        new(new JsonSerializerOptions { Converters = { new SchemaOrRefConverter() } });

    [Test]
    public async Task ParameterlessConstructor_IsDefault()
    {
        var value = new SchemaOrRef();

        await Assert.That(value.IsDefault).IsTrue();
        await Assert.That(value).IsEqualTo(default);
    }

    [Test]
    [Arguments("inline")]
    [Arguments("ref")]
    public async Task RoundTrip_UsesRuntimeConverter(string kind)
    {
        var credentials = kind == "inline" ? SchemaOrRef.Inline("{}") : SchemaOrRef.Ref("artifacts/auth");
        var product = new Product { Credentials = credentials };
        var context = CreateContext();

        var json = JsonSerializer.Serialize(product, context.Product);
        var roundTripped = JsonSerializer.Deserialize(json, context.Product);

        await Assert.That(roundTripped).IsNotNull();
        await Assert.That(roundTripped!.Credentials).IsEqualTo(credentials);
    }

    [Test]
    public async Task RoundTrip_Null()
    {
        var context = CreateContext();

        var json = JsonSerializer.Serialize(new Product(), context.Product);
        var roundTripped = JsonSerializer.Deserialize(json, context.Product);

        await Assert.That(json).IsEqualTo("""{"Credentials":null}""");
        await Assert.That(roundTripped!.Credentials).IsNull();
    }
}
