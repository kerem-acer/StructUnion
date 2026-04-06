using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

static class MatchEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        EmitFuncMatch(sb, model);
        sb.AppendLine();
        EmitActionMatch(sb, model);
    }

    static void EmitFuncMatch(SourceBuilder sb, UnionModel model)
    {
        var tag = model.TagField;

        var funcParams = new List<string>();
        foreach (var variant in model.Variants)
        {
            if (variant.Parameters.Count == 0)
            {
                funcParams.Add($"global::System.Func<TResult> {CSharpIdentifiers.ToCamelCase(variant.Name)}");
            }
            else
            {
                var types = variant.Parameters.Select(p => p.TypeFullyQualified).ToList();
                types.Add("TResult");
                var funcType = $"global::System.Func<{string.Join(", ", types)}>";
                funcParams.Add($"{funcType} {CSharpIdentifiers.ToCamelCase(variant.Name)}");
            }
        }

        sb.AppendLine($"public TResult Match<TResult>({string.Join(", ", funcParams)})");
        using (sb.Block())
        {
            sb.AppendLine($"return {tag} switch");
            sb.OpenBrace();
            foreach (var variant in model.Variants)
            {
                var args = string.Join(", ", variant.Parameters.Select(p =>
                    model.VariantField(variant.Name, p.Name)));
                sb.AppendLine($"Tag{variant.Name} => {CSharpIdentifiers.ToCamelCase(variant.Name)}({args}),");
            }
            sb.AppendLine("_ => ThrowUnknownTag<TResult>()");
            sb.CloseBraceNoNewline();
            sb.AppendLine(";");
        }
    }

    static void EmitActionMatch(SourceBuilder sb, UnionModel model)
    {
        var tag = model.TagField;

        var actionParams = new List<string>();
        foreach (var variant in model.Variants)
        {
            if (variant.Parameters.Count == 0)
            {
                actionParams.Add($"global::System.Action {CSharpIdentifiers.ToCamelCase(variant.Name)}");
            }
            else
            {
                var types = string.Join(", ", variant.Parameters.Select(p => p.TypeFullyQualified));
                actionParams.Add($"global::System.Action<{types}> {CSharpIdentifiers.ToCamelCase(variant.Name)}");
            }
        }

        sb.AppendLine($"public void Match({string.Join(", ", actionParams)})");
        using (sb.Block())
        {
            sb.AppendLine($"switch ({tag})");
            using (sb.Block())
            {
                foreach (var variant in model.Variants)
                {
                    var args = string.Join(", ", variant.Parameters.Select(p =>
                        model.VariantField(variant.Name, p.Name)));
                    sb.AppendLine($"case Tag{variant.Name}: {CSharpIdentifiers.ToCamelCase(variant.Name)}({args}); break;");
                }
                sb.AppendLine("default: ThrowUnknownTag<int>(); break;");
            }
        }
    }
}
