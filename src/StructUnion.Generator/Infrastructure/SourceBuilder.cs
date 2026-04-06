using System.Text;

namespace StructUnion.Generator.Infrastructure;

/// <summary>
/// Indentation-aware string builder for source code generation.
/// </summary>
sealed class SourceBuilder
{
    readonly StringBuilder _sb = new();
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
        _indent--;
        AppendLine(semicolon ? "};" : "}");
        return this;
    }

    public SourceBuilder CloseBraceNoNewline()
    {
        _indent--;
        WriteIndent();
        _sb.Append('}');
        return this;
    }

    public IDisposable Block()
    {
        OpenBrace();
        return new BlockScope(this);
    }

    public IDisposable Indent()
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
        for (var i = 0; i < _indent; i++)
        {
            _sb.Append("    ");
        }
    }

    public override string ToString() => _sb.ToString();

    sealed class BlockScope(SourceBuilder builder) : IDisposable
    {
        public void Dispose() => builder.CloseBrace();
    }

    sealed class IndentScope(SourceBuilder builder) : IDisposable
    {
        public void Dispose() => builder._indent--;
    }
}
