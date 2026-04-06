using StructUnion.Generator.Infrastructure;

namespace StructUnion.Generator.Models;

readonly record struct VariantModel(
    string Name,
    EquatableArray<FieldModel> Parameters,
    byte Tag);
