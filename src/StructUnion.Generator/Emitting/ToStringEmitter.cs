using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

static class ToStringEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        var tag = model.TagField;

        sb.AppendLine("public override string ToString()");
        using (sb.Block())
        {
            sb.AppendLine($"return {tag} switch");
            sb.OpenBrace();
            foreach (var variant in model.Variants)
            {
                if (variant.Parameters.Count == 0)
                {
                    sb.AppendLine($"Tags.{variant.Name} => \"{variant.Name}\",");
                    continue;
                }

                var parts = variant.Parameters.Select(p =>
                {
                    var field = model.VariantField(variant.Name, p.Name);
                    return $"{{{field}}}";
                });
                sb.AppendLine($"Tags.{variant.Name} => $\"{variant.Name}({string.Join(", ", parts)})\",");
            }
            sb.AppendLine("Tags.Default => \"Default\",");
            sb.AppendLine("_ => \"<invalid>\"");
            sb.CloseBraceNoNewline();
            sb.AppendLine(";");
        }
    }
}
