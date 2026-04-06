using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using StructUnion.Generator.Infrastructure;

namespace StructUnion.Generator.Models;

readonly record struct DiagnosticInfo(
    string Id,
    string FilePath,
    int SpanStart,
    int SpanLength,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter,
    EquatableArray<string> MessageArgs)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location location, params string[] messageArgs)
    {
        var span = location.SourceSpan;
        var lineSpan = location.GetLineSpan().Span;
        return new DiagnosticInfo(
            descriptor.Id,
            location.SourceTree?.FilePath ?? "",
            span.Start,
            span.Length,
            lineSpan.Start.Line,
            lineSpan.Start.Character,
            lineSpan.End.Line,
            lineSpan.End.Character,
            messageArgs.ToImmutableArray().ToEquatableArray());
    }

    public Diagnostic ToDiagnostic()
    {
        var descriptor = DiagnosticDescriptors.GetById(Id);
        var location = Location.Create(
            FilePath,
            new TextSpan(SpanStart, SpanLength),
            new LinePositionSpan(
                new LinePosition(StartLine, StartCharacter),
                new LinePosition(EndLine, EndCharacter)));
        var args = new object[MessageArgs.Count];
        for (var i = 0; i < MessageArgs.Count; i++)
        {
            args[i] = MessageArgs[i];
        }

        return Diagnostic.Create(descriptor, location, args);
    }
}
