using StructUnion.Generator.Infrastructure;

namespace StructUnion.Generator.Models;

readonly record struct TypeParameterModel(
    string Name,
    EquatableArray<string> Constraints);
