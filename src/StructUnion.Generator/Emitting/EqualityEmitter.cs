using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

static class EqualityEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        var typeName = model.TypeNameWithParameters;
        var tag = model.TagField;

        // Equals(T other)
        sb.AppendLine($"public bool Equals({typeName} other)");
        using (sb.Block())
        {
            sb.AppendLine($"if ({tag} != other.{tag}) return false;");

            // Common fields
            foreach (var field in model.CommonFields)
            {
                sb.AppendLine($"if (!global::System.Collections.Generic.EqualityComparer<{field.TypeFullyQualified}>.Default.Equals({field.Name}, other.{field.Name})) return false;");
            }

            sb.AppendLine($"return {tag} switch");
            sb.OpenBrace();
            foreach (var variant in model.Variants)
            {
                if (variant.Parameters.Count == 0)
                {
                    sb.AppendLine($"Tag{variant.Name} => true,");
                    continue;
                }

                var comparisons = variant.Parameters.Select(p =>
                {
                    var fn = model.VariantField(variant.Name, p.Name);
                    return $"global::System.Collections.Generic.EqualityComparer<{p.TypeFullyQualified}>.Default.Equals({fn}, other.{fn})";
                });
                sb.AppendLine($"Tag{variant.Name} => {string.Join(" && ", comparisons)},");
            }
            sb.AppendLine("_ => true");
            sb.CloseBraceNoNewline();
            sb.AppendLine(";");
        }

        sb.AppendLine();

        // Equals(object?)
        sb.AppendLine($"public override bool Equals(object? obj) => obj is {typeName} other && Equals(other);");
        sb.AppendLine();

        // GetHashCode
        sb.AppendLine("public override int GetHashCode()");
        using (sb.Block())
        {
            var needsBuilder = false;
            foreach (var variant in model.Variants)
            {
                if (1 + model.CommonFields.Count + variant.Parameters.Count > 8)
                {
                    needsBuilder = true;
                    break;
                }
            }

            if (needsBuilder)
            {
                sb.AppendLine($"var hash = new global::System.HashCode();");
                sb.AppendLine($"hash.Add({tag});");
                foreach (var field in model.CommonFields)
                {
                    sb.AppendLine($"hash.Add({field.Name});");
                }

                sb.AppendLine($"switch ({tag})");
                using (sb.Block())
                {
                    foreach (var variant in model.Variants)
                    {
                        if (variant.Parameters.Count == 0)
                        {
                            continue;
                        }

                        sb.AppendLine($"case Tag{variant.Name}:");
                        using (sb.Indent())
                        {
                            foreach (var param in variant.Parameters)
                            {
                                sb.AppendLine($"hash.Add({model.VariantField(variant.Name, param.Name)});");
                            }

                            sb.AppendLine("break;");
                        }
                    }
                }

                sb.AppendLine("return hash.ToHashCode();");
            }
            else
            {
                sb.AppendLine($"return {tag} switch");
                sb.OpenBrace();
                foreach (var variant in model.Variants)
                {
                    var hashParts = new List<string> { tag };
                    foreach (var field in model.CommonFields)
                    {
                        hashParts.Add(field.Name);
                    }

                    foreach (var param in variant.Parameters)
                    {
                        hashParts.Add(model.VariantField(variant.Name, param.Name));
                    }

                    sb.AppendLine($"Tag{variant.Name} => global::System.HashCode.Combine({string.Join(", ", hashParts)}),");
                }
                sb.AppendLine($"_ => {tag}.GetHashCode()");
                sb.CloseBraceNoNewline();
                sb.AppendLine(";");
            }
        }

        sb.AppendLine();

        // Operators
        sb.AppendLine($"public static bool operator ==({typeName} left, {typeName} right) => left.Equals(right);");
        sb.AppendLine($"public static bool operator !=({typeName} left, {typeName} right) => !left.Equals(right);");
    }
}
