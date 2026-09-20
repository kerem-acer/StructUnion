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
                    var field = variant.FieldName(p.Name);

                    // A ref-like type with no ToString() override of its own cannot be rendered at
                    // all: the inherited object.ToString() would box the receiver. Name the field
                    // instead, matching the "<invalid>" placeholder style below.
                    if (!p.IsRenderable)
                    {
                        return $"<{p.Name}>";
                    }

                    // A bare hole routes through the handler's generic AppendFormatted<T>, which a
                    // ref-like type cannot satisfy — so call ToString() and hand over a string.
                    return p.IsRefLike ? $"{{{field}.ToString()}}" : $"{{{field}}}";
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
