using System.Diagnostics;
using System.Text;

namespace StructUnion.Generator.Infrastructure;

/// <summary>
/// Indentation-aware string builder for source code generation.
/// </summary>
sealed class SourceBuilder
{
    static readonly string[] IndentStrings = CreateIndentStrings(16);

    static string[] CreateIndentStrings(int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
            result[i] = new string(' ', i * 4);
        return result;
    }

    readonly StringBuilder _sb = new(4096);
    int _indent;
    bool _needsIndent = true;

    public SourceBuilder AppendLine()
    {
        _sb.AppendLine();
        _needsIndent = true;
        return this;
    }

    public SourceBuilder AppendLine(string text)
    {
        WriteIndent();
        _sb.AppendLine(text);
        _needsIndent = true;
        return this;
    }

    public SourceBuilder Append(string text)
    {
        WriteIndent();
        _sb.Append(text);
        return this;
    }

    public SourceBuilder OpenBrace()
    {
        AppendLine("{");
        _indent++;
        return this;
    }

    public SourceBuilder CloseBrace(bool semicolon = false)
    {
        Debug.Assert(_indent > 0, "CloseBrace called with zero indent");
        _indent--;
        AppendLine(semicolon ? "};" : "}");
        return this;
    }

    public SourceBuilder CloseBraceNoNewline()
    {
        Debug.Assert(_indent > 0, "CloseBraceNoNewline called with zero indent");
        _indent--;
        WriteIndent();
        _sb.Append('}');
        return this;
    }

    public BlockScope Block()
    {
        OpenBrace();
        return new BlockScope(this);
    }

    public IndentScope Indent()
    {
        _indent++;
        return new IndentScope(this);
    }

    void WriteIndent()
    {
        if (!_needsIndent)
        {
            return;
        }

        _needsIndent = false;
        if (_indent > 0)
        {
            _sb.Append(_indent < IndentStrings.Length
                ? IndentStrings[_indent]
                : new string(' ', _indent * 4));
        }
    }

    public override string ToString() => _sb.ToString();

    internal ref struct BlockScope(SourceBuilder builder)
    {
        public void Dispose() => builder.CloseBrace();
    }

    internal ref struct IndentScope(SourceBuilder builder)
    {
        public void Dispose()
        {
            Debug.Assert(builder._indent > 0, "IndentScope.Dispose called with zero indent");
            builder._indent--;
        }
    }
}
