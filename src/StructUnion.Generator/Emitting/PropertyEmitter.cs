using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

static class PropertyEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        var tag = model.TagField;

        sb.AppendLine("/// <summary>Gets the tag value identifying which variant is active.</summary>");
        sb.AppendLine($"public byte Tag => {tag};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Returns true if this is a default-constructed instance with no active variant.</summary>");
        sb.AppendLine($"public bool IsDefault => {tag} == 0;");
        sb.AppendLine();

        foreach (var variant in model.Variants)
        {
            sb.AppendLine($"public bool Is{variant.Name} => {tag} == Tag{variant.Name};");
        }

        sb.AppendLine();

        foreach (var variant in model.Variants)
        {
            foreach (var param in variant.Parameters)
            {
                var propName = $"{variant.Name}{char.ToUpperInvariant(param.Name[0])}{param.Name.Substring(1)}";
                var field = model.VariantField(variant.Name, param.Name);

                sb.AppendLine($"public {param.TypeFullyQualified} {propName}");
                using (sb.Block())
                {
                    sb.AppendLine("get");
                    using (sb.Block())
                    {
                        sb.AppendLine($"if ({tag} != Tag{variant.Name}) ThrowInvalidCase(nameof({variant.Name}));");
                        sb.AppendLine($"return {field};");
                    }
                }
            }
        }

        sb.AppendLine();

        foreach (var variant in model.Variants)
        {
            EmitTryGet(sb, model, variant);
        }
    }

    static void EmitTryGet(SourceBuilder sb, UnionModel model, VariantModel variant)
    {
        var tag = model.TagField;

        if (variant.Parameters.Count == 0)
        {
            sb.AppendLine($"public bool TryGet{variant.Name}() => {tag} == Tag{variant.Name};");
            sb.AppendLine();
            return;
        }

        var outParams = string.Join(", ", variant.Parameters.Select(p =>
            $"out {p.TypeFullyQualified} {CSharpIdentifiers.EscapeKeyword(p.Name)}"));

        sb.AppendLine($"public bool TryGet{variant.Name}({outParams})");
        using (sb.Block())
        {
            sb.AppendLine($"if ({tag} == Tag{variant.Name})");
            using (sb.Block())
            {
                foreach (var param in variant.Parameters)
                {
                    var field = model.VariantField(variant.Name, param.Name);
                    sb.AppendLine($"{CSharpIdentifiers.EscapeKeyword(param.Name)} = {field};");
                }
                sb.AppendLine("return true;");
            }
            sb.AppendLine();

            foreach (var param in variant.Parameters)
            {
                sb.AppendLine($"{CSharpIdentifiers.EscapeKeyword(param.Name)} = default!;");
            }

            sb.AppendLine("return false;");
        }
        sb.AppendLine();
    }
}
