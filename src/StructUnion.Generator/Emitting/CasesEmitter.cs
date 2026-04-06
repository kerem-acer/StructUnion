using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

static class CasesEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        sb.AppendLine("public static class Cases");
        using (sb.Block())
        {
            foreach (var variant in model.Variants)
            {
                if (variant.Parameters.Count == 0)
                {
                    continue;
                }

                EmitVariantStruct(sb, variant);
                sb.AppendLine();
            }
        }

        sb.AppendLine();
    }

    static void EmitVariantStruct(SourceBuilder sb, VariantModel variant)
    {
        sb.AppendLine($"public readonly struct {variant.Name}");
        using (sb.Block())
        {
            foreach (var param in variant.Parameters)
            {
                var propName = $"{char.ToUpperInvariant(param.Name[0])}{param.Name.Substring(1)}";
                sb.AppendLine($"public {param.TypeFullyQualified} {propName} {{ get; }}");
            }

            sb.AppendLine();

            var ctorParams = string.Join(", ", variant.Parameters.Select(p =>
                $"{p.TypeFullyQualified} {CSharpIdentifiers.ToCamelCase(p.Name)}"));

            sb.AppendLine($"public {variant.Name}({ctorParams})");
            using (sb.Block())
            {
                foreach (var param in variant.Parameters)
                {
                    var propName = $"{char.ToUpperInvariant(param.Name[0])}{param.Name.Substring(1)}";
                    sb.AppendLine($"{propName} = {CSharpIdentifiers.ToCamelCase(param.Name)};");
                }
            }
        }
    }
}
