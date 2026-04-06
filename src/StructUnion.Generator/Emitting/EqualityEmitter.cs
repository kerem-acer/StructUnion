using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

static class EqualityEmitter
{
    /// <summary>
    /// Types where <c>==</c> is equivalent to <c>EqualityComparer&lt;T&gt;.Default.Equals()</c>.
    /// Excludes float/double/decimal where NaN semantics differ.
    /// </summary>
    static readonly HashSet<string> DirectEqualityTypes =
    [
        "bool", "byte", "sbyte", "short", "ushort", "char",
        "int", "uint", "long", "ulong", "nint", "nuint"
    ];

    /// <summary>
    /// Types where instance <c>.Equals()</c> handles NaN correctly (NaN == NaN → true)
    /// without the virtual dispatch overhead of <c>EqualityComparer&lt;T&gt;.Default</c>.
    /// </summary>
    static readonly HashSet<string> InstanceEqualsTypes = ["float", "double"];

    static string EmitFieldComparison(string fieldExpr, string otherFieldExpr, string typeFullyQualified) =>
        DirectEqualityTypes.Contains(typeFullyQualified)
            ? $"{fieldExpr} == other.{otherFieldExpr}"
            : InstanceEqualsTypes.Contains(typeFullyQualified)
                ? $"{fieldExpr}.Equals(other.{otherFieldExpr})"
                : $"global::System.Collections.Generic.EqualityComparer<{typeFullyQualified}>.Default.Equals({fieldExpr}, other.{otherFieldExpr})";

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
                var cmp = EmitFieldComparison(field.Name, field.Name, field.TypeFullyQualified);
                sb.AppendLine($"if (!({cmp})) return false;");
            }

            sb.AppendLine($"return {tag} switch");
            sb.OpenBrace();
            foreach (var variant in model.Variants)
            {
                if (variant.Parameters.Count == 0)
                {
                    sb.AppendLine($"Tags.{variant.Name} => true,");
                    continue;
                }

                var comparisons = variant.Parameters.Select(p =>
                {
                    var fn = variant.FieldName(p.Name);
                    return EmitFieldComparison(fn, fn, p.TypeFullyQualified);
                });
                sb.AppendLine($"Tags.{variant.Name} => {string.Join(" && ", comparisons)},");
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

                        sb.AppendLine($"case Tags.{variant.Name}:");
                        using (sb.Indent())
                        {
                            foreach (var param in variant.Parameters)
                            {
                                sb.AppendLine($"hash.Add({variant.FieldName(param.Name)});");
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
                        hashParts.Add(variant.FieldName(param.Name));
                    }

                    sb.AppendLine($"Tags.{variant.Name} => global::System.HashCode.Combine({string.Join(", ", hashParts)}),");
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
