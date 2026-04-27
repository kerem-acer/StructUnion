using StructUnion.Generator.Infrastructure;

namespace StructUnion.Generator.Models;

/// <summary>
/// Result from the per-type transform phase (no assembly options involved).
/// If <see cref="Data"/> is null, structural validation failed — only report diagnostics.
/// </summary>
readonly record struct TransformResult(
    TypeExtract? Data,
    EquatableArray<DiagnosticInfo> Diagnostics);

/// <summary>
/// Per-type data extracted from Roslyn symbols during the transform phase.
/// Assembly options are NOT resolved yet — that happens after Combine.
/// </summary>
readonly record struct TypeExtract(
    string Namespace,
    EquatableArray<string> ContainingTypes,
    string Accessibility,
    string SymbolName,
    EquatableArray<TypeParameterModel> TypeParameters,
    EquatableArray<VariantModel> Variants,
    EquatableArray<FieldModel> CommonFields,
    GenerationMode Mode,
    string TemplateTypeKeyword,
    // Per-type attribute props (unresolved — cascade happens later)
    bool? PerTypeImplicit,
    string? GeneratedName,
    string? PerTypeTag,
    bool? PerTypeNested,
    string? PerTypeSuffix,
    bool? PerTypeGenerateDispose,
    // Serialized location for deferred diagnostics
    DiagnosticLocation Location);

/// <summary>
/// Serialized source location for diagnostics that must be reported
/// after the transform phase (when Roslyn Location is no longer available).
/// </summary>
readonly record struct DiagnosticLocation(
    string FilePath,
    int SpanStart,
    int SpanLength,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter)
{
    public static DiagnosticLocation From(Microsoft.CodeAnalysis.Location location)
    {
        var span = location.SourceSpan;
        var lineSpan = location.GetLineSpan().Span;
        return new(
            location.SourceTree?.FilePath ?? "",
            span.Start, span.Length,
            lineSpan.Start.Line, lineSpan.Start.Character,
            lineSpan.End.Line, lineSpan.End.Character);
    }
}
