using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

static class MatchEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        EmitFuncMatch(sb, model);
        sb.AppendLine();
        EmitStateFuncMatch(sb, model);
        sb.AppendLine();
        EmitActionMatch(sb, model);
        sb.AppendLine();
        EmitStateActionMatch(sb, model);
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
                    variant.FieldName(p.Name)));
                sb.AppendLine($"Tags.{variant.Name} => {CSharpIdentifiers.ToCamelCase(variant.Name)}({args}),");
            }
            sb.AppendLine("_ => ThrowUnknownTag<TResult>()");
            sb.CloseBraceNoNewline();
            sb.AppendLine(";");
        }
    }

    static void EmitStateFuncMatch(SourceBuilder sb, UnionModel model)
    {
        var tag = model.TagField;

        var funcParams = new List<string> { "TState state" };
        foreach (var variant in model.Variants)
        {
            var types = new List<string> { "TState" };
            for (var i = 0; i < variant.Parameters.Count; i++)
            {
                types.Add(variant.Parameters[i].TypeFullyQualified);
            }

            types.Add("TResult");
            var funcType = $"global::System.Func<{string.Join(", ", types)}>";
            funcParams.Add($"{funcType} {CSharpIdentifiers.ToCamelCase(variant.Name)}");
        }

        sb.AppendLine($"public TResult Match<TState, TResult>({string.Join(", ", funcParams)})");
        using (sb.Block())
        {
            sb.AppendLine($"return {tag} switch");
            sb.OpenBrace();
            foreach (var variant in model.Variants)
            {
                var args = new List<string> { "state" };
                for (var i = 0; i < variant.Parameters.Count; i++)
                {
                    args.Add(variant.FieldName(variant.Parameters[i].Name));
                }

                sb.AppendLine($"Tags.{variant.Name} => {CSharpIdentifiers.ToCamelCase(variant.Name)}({string.Join(", ", args)}),");
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
                        variant.FieldName(p.Name)));
                    sb.AppendLine($"case Tags.{variant.Name}: {CSharpIdentifiers.ToCamelCase(variant.Name)}({args}); break;");
                }
                sb.AppendLine("default: ThrowUnknownTag(); break;");
            }
        }
    }

    static void EmitStateActionMatch(SourceBuilder sb, UnionModel model)
    {
        var tag = model.TagField;

        var actionParams = new List<string> { "TState state" };
        foreach (var variant in model.Variants)
        {
            var types = new List<string> { "TState" };
            for (var i = 0; i < variant.Parameters.Count; i++)
            {
                types.Add(variant.Parameters[i].TypeFullyQualified);
            }

            var actionType = $"global::System.Action<{string.Join(", ", types)}>";
            actionParams.Add($"{actionType} {CSharpIdentifiers.ToCamelCase(variant.Name)}");
        }

        sb.AppendLine($"public void Match<TState>({string.Join(", ", actionParams)})");
        using (sb.Block())
        {
            sb.AppendLine($"switch ({tag})");
            using (sb.Block())
            {
                foreach (var variant in model.Variants)
                {
                    var args = new List<string> { "state" };
                    for (var i = 0; i < variant.Parameters.Count; i++)
                    {
                        args.Add(variant.FieldName(variant.Parameters[i].Name));
                    }

                    sb.AppendLine($"case Tags.{variant.Name}: {CSharpIdentifiers.ToCamelCase(variant.Name)}({string.Join(", ", args)}); break;");
                }

                sb.AppendLine("default: ThrowUnknownTag(); break;");
            }
        }
    }
}
