using StructUnion.Generator.Infrastructure;

namespace StructUnion.UnitTests;

public class CSharpIdentifiersTests
{
    [Test]
    [Arguments("int", "@int")]
    [Arguments("class", "@class")]
    [Arguments("bool", "@bool")]
    [Arguments("base", "@base")]
    [Arguments("string", "@string")]
    public async Task EscapeKeyword_Keywords_AddPrefix(string input, string expected)
    {
        await Assert.That(CSharpIdentifiers.EscapeKeyword(input)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("name", "name")]
    [Arguments("radius", "radius")]
    [Arguments("MyType", "MyType")]
    public async Task EscapeKeyword_NonKeywords_Unchanged(string input, string expected)
    {
        await Assert.That(CSharpIdentifiers.EscapeKeyword(input)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Name", "name")]
    [Arguments("Radius", "radius")]
    [Arguments("X", "x")]
    public async Task ToCamelCase_LowercasesFirstChar(string input, string expected)
    {
        await Assert.That(CSharpIdentifiers.ToCamelCase(input)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Bool", "@bool")]
    [Arguments("Int", "@int")]
    [Arguments("Base", "@base")]
    [Arguments("String", "@string")]
    public async Task ToCamelCase_EscapesKeywords(string input, string expected)
    {
        await Assert.That(CSharpIdentifiers.ToCamelCase(input)).IsEqualTo(expected);
    }

    [Test]
    public async Task EscapeKeyword_EmptyString_Unchanged()
    {
        await Assert.That(CSharpIdentifiers.EscapeKeyword("")).IsEqualTo("");
    }

    [Test]
    public async Task ToCamelCase_EmptyString_ReturnsEmpty()
    {
        await Assert.That(CSharpIdentifiers.ToCamelCase("")).IsEqualTo("");
    }

    [Test]
    public async Task ToCamelCase_SingleChar()
    {
        await Assert.That(CSharpIdentifiers.ToCamelCase("X")).IsEqualTo("x");
    }

    [Test]
    public async Task ToCamelCase_AlreadyCamelCase_Unchanged()
    {
        await Assert.That(CSharpIdentifiers.ToCamelCase("name")).IsEqualTo("name");
    }

    [Test]
    public async Task EscapeKeyword_AllCommonKeywords()
    {
        var keywords = new[] { "void", "while", "for", "if", "return", "new", "null", "true", "false", "this" };
        foreach (var kw in keywords)
        {
            await Assert.That(CSharpIdentifiers.EscapeKeyword(kw)).IsEqualTo($"@{kw}");
        }
    }
}
