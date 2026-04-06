using StructUnion.Generator.Infrastructure;

namespace StructUnion.Generator.Models;

readonly record struct ParseResult(
    UnionModel? Model,
    EquatableArray<DiagnosticInfo> Diagnostics);
