using StructUnion.Generator.Infrastructure;

namespace StructUnion.UnitTests;

public class SourceBuilderTests
{
    [Test]
    public async Task Empty_ToString_ReturnsEmpty()
    {
        var sb = new SourceBuilder();
        await Assert.That(sb.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task AppendLine_NoIndent()
    {
        var sb = new SourceBuilder();
        sb.AppendLine("hello");
        await Assert.That(sb.ToString()).IsEqualTo("hello\n");
    }

    [Test]
    public async Task AppendLine_Empty_AddsNewline()
    {
        var sb = new SourceBuilder();
        sb.AppendLine();
        await Assert.That(sb.ToString()).IsEqualTo("\n");
    }

    [Test]
    public async Task Append_NoTrailingNewline()
    {
        var sb = new SourceBuilder();
        sb.Append("hello");
        await Assert.That(sb.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task OpenBrace_IncreasesIndent()
    {
        var sb = new SourceBuilder();
        sb.AppendLine("class Foo");
        sb.OpenBrace();
        sb.AppendLine("int x;");
        sb.CloseBrace();

        var result = sb.ToString();
        await Assert.That(result).Contains("{\n");
        await Assert.That(result).Contains("    int x;\n");
        await Assert.That(result).Contains("}\n");
    }

    [Test]
    public async Task NestedBlocks_DoubleIndent()
    {
        var sb = new SourceBuilder();
        sb.OpenBrace();
        sb.OpenBrace();
        sb.AppendLine("deep");
        sb.CloseBrace();
        sb.CloseBrace();

        await Assert.That(sb.ToString()).Contains("        deep\n");
    }

    [Test]
    public async Task CloseBrace_WithSemicolon()
    {
        var sb = new SourceBuilder();
        sb.OpenBrace();
        sb.CloseBrace(semicolon: true);

        await Assert.That(sb.ToString()).Contains("};\n");
    }

    [Test]
    public async Task Block_Disposable_AutoCloses()
    {
        var sb = new SourceBuilder();
        using (sb.Block())
        {
            sb.AppendLine("inside");
        }

        var result = sb.ToString();
        await Assert.That(result).Contains("{\n");
        await Assert.That(result).Contains("    inside\n");
        await Assert.That(result).Contains("}\n");
    }

    [Test]
    public async Task Indent_Disposable_IncrementsAndDecrements()
    {
        var sb = new SourceBuilder();
        sb.AppendLine("before");
        using (sb.Indent())
        {
            sb.AppendLine("indented");
        }
        sb.AppendLine("after");

        var result = sb.ToString();
        await Assert.That(result).Contains("before\n");
        await Assert.That(result).Contains("    indented\n");
        await Assert.That(result).Contains("after\n");
    }

    [Test]
    public async Task NestedBlock_CorrectIndentation()
    {
        var sb = new SourceBuilder();
        sb.AppendLine("namespace Foo");
        using (sb.Block())
        {
            sb.AppendLine("class Bar");
            using (sb.Block())
            {
                sb.AppendLine("int x;");
            }
        }

        var result = sb.ToString();
        await Assert.That(result).Contains("    class Bar\n");
        await Assert.That(result).Contains("        int x;\n");
    }

    [Test]
    public async Task MultipleAppendLine_CorrectOrder()
    {
        var sb = new SourceBuilder();
        sb.AppendLine("a");
        sb.AppendLine("b");
        sb.AppendLine("c");

        await Assert.That(sb.ToString()).IsEqualTo("a\nb\nc\n");
    }

    [Test]
    public async Task Append_ThenAppendLine_SameLine()
    {
        var sb = new SourceBuilder();
        sb.Append("hello ");
        sb.AppendLine("world");

        await Assert.That(sb.ToString()).IsEqualTo("hello world\n");
    }

    [Test]
    public async Task CloseBraceNoNewline_DoesNotAddNewline()
    {
        var sb = new SourceBuilder();
        sb.OpenBrace();
        sb.CloseBraceNoNewline();

        var result = sb.ToString();
        await Assert.That(result).IsEqualTo("{\n}");
    }
}
