using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

static class CasesEmitter
{
    const string Inline = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";

    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        sb.AppendLine("public static class Cases");
        using (sb.Block())
        {
            foreach (var variant in model.Variants)
            {
                // A native union needs a case type for every variant, including the empty ones.
                // Nested-accessor mode has no use for an empty struct, so it keeps skipping them.
                if (variant.Parameters.Count == 0 && !model.NativeUnion)
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
        // An empty variant carries nothing, so it needs no constructor and nothing to deconstruct.
        if (variant.Parameters.Count == 0)
        {
            sb.AppendLine($"public readonly struct {variant.Name}");
            using (sb.Block())
            {
            }

            return;
        }

        sb.AppendLine($"public readonly struct {variant.Name}");
        using (sb.Block())
        {
            foreach (var param in variant.Parameters)
            {
                var propName = CSharpIdentifiers.ToPascalCase(param.Name);
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
                    var propName = CSharpIdentifiers.ToPascalCase(param.Name);
                    sb.AppendLine($"{propName} = {CSharpIdentifiers.ToCamelCase(param.Name)};");
                }
            }

            sb.AppendLine();

            // Enables positional patterns: `Shape.Cases.Rectangle(var length, var width)`.
            var outParams = string.Join(", ", variant.Parameters.Select(p =>
                $"out {p.TypeFullyQualified} {CSharpIdentifiers.ToCamelCase(p.Name)}"));

            sb.AppendLine(Inline);
            sb.AppendLine($"public void Deconstruct({outParams})");
            using (sb.Block())
            {
                foreach (var param in variant.Parameters)
                {
                    var propName = CSharpIdentifiers.ToPascalCase(param.Name);
                    sb.AppendLine($"{CSharpIdentifiers.ToCamelCase(param.Name)} = {propName};");
                }
            }
        }
    }
}
