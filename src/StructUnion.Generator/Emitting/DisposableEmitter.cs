using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

/// <summary>
/// Emits IDisposable / IAsyncDisposable implementations and per-variant Take/TryTake helpers
/// when the union opts in via <c>[StructUnion(GenerateDispose = true)]</c>.
/// </summary>
static class DisposableEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        if (!model.GenerateDispose)
        {
            return;
        }

        if (model.HasAnySyncDisposable)
        {
            EmitDispose(sb, model);
            sb.AppendLine();
        }

        if (model.HasAnyAsyncDisposable)
        {
            EmitDisposeAsync(sb, model);
            sb.AppendLine();
        }

        foreach (var variant in model.Variants)
        {
            if (!UnionModel.VariantNeedsDisposal(variant))
            {
                continue;
            }

            EmitTake(sb, model, variant);
            sb.AppendLine();
            EmitTryTake(sb, model, variant);
            sb.AppendLine();
        }
    }

    static void EmitDispose(SourceBuilder sb, UnionModel model)
    {
        sb.AppendLine("/// <summary>Disposes the active variant's IDisposable resources, if any. Safe to call on default and non-disposable variants.</summary>");
        sb.AppendLine("public void Dispose()");
        using (sb.Block())
        {
            sb.AppendLine($"switch ({model.TagField})");
            using (sb.Block())
            {
                foreach (var variant in model.Variants)
                {
                    if (!HasAnySyncField(variant))
                    {
                        continue;
                    }

                    sb.AppendLine($"case Tags.{variant.Name}:");
                    using (sb.Indent())
                    {
                        foreach (var param in variant.Parameters)
                        {
                            if (param.IsDisposable)
                            {
                                EmitFieldSyncDispose(sb, variant, param);
                            }
                        }
                        sb.AppendLine("break;");
                    }
                }
            }
        }
    }

    static void EmitDisposeAsync(SourceBuilder sb, UnionModel model)
    {
        sb.AppendLine("/// <summary>Asynchronously disposes the active variant's IAsyncDisposable / IDisposable resources, if any. Safe to call on default and non-disposable variants.</summary>");
        sb.AppendLine("public async global::System.Threading.Tasks.ValueTask DisposeAsync()");
        using (sb.Block())
        {
            sb.AppendLine($"switch ({model.TagField})");
            using (sb.Block())
            {
                foreach (var variant in model.Variants)
                {
                    if (!UnionModel.VariantNeedsDisposal(variant))
                    {
                        continue;
                    }

                    sb.AppendLine($"case Tags.{variant.Name}:");
                    using (sb.Indent())
                    {
                        foreach (var param in variant.Parameters)
                        {
                            if (param.IsAsyncDisposable)
                            {
                                EmitFieldAsyncDispose(sb, variant, param);
                            }
                            else if (param.IsDisposable)
                            {
                                EmitFieldSyncDispose(sb, variant, param);
                            }
                        }
                        sb.AppendLine("break;");
                    }
                }
            }
        }
    }

    static void EmitFieldSyncDispose(SourceBuilder sb, VariantModel variant, FieldModel param)
    {
        var field = variant.FieldName(param.Name);
        if (param.IsValueType)
        {
            sb.AppendLine($"{field}.Dispose();");
        }
        else
        {
            sb.AppendLine($"{field}?.Dispose();");
        }
    }

    static void EmitFieldAsyncDispose(SourceBuilder sb, VariantModel variant, FieldModel param)
    {
        var field = variant.FieldName(param.Name);
        if (param.IsValueType)
        {
            sb.AppendLine($"await {field}.DisposeAsync().ConfigureAwait(false);");
        }
        else
        {
            sb.AppendLine($"if ({field} is not null) await {field}.DisposeAsync().ConfigureAwait(false);");
        }
    }

    static void EmitTake(SourceBuilder sb, UnionModel model, VariantModel variant)
    {
        // Single-field variant → return value directly
        if (variant.Parameters.Count == 1)
        {
            var param = variant.Parameters[0];
            var field = variant.FieldName(param.Name);
            sb.AppendLine($"/// <summary>Removes ownership of the {variant.Name} payload from <paramref name=\"self\"/> and returns it. Sets <paramref name=\"self\"/> to default so subsequent disposal is a no-op. Throws if the active variant is not {variant.Name}.</summary>");
            sb.AppendLine($"public static {param.TypeFullyQualified} Take{variant.Name}(ref {model.TypeNameWithParameters} self)");
            using (sb.Block())
            {
                sb.AppendLine($"if (self.{model.TagField} != Tags.{variant.Name}) ThrowInvalidCase(nameof({variant.Name}));");
                sb.AppendLine($"var value = self.{field};");
                sb.AppendLine("self = default;");
                sb.AppendLine("return value;");
            }
            return;
        }

        // Multi-field → out parameters
        var outParams = string.Join(", ", variant.Parameters.Select(p =>
            $"out {p.TypeFullyQualified} {CSharpIdentifiers.EscapeKeyword(p.Name)}"));

        sb.AppendLine($"/// <summary>Removes ownership of the {variant.Name} payload from <paramref name=\"self\"/> and writes its fields to the out parameters. Sets <paramref name=\"self\"/> to default so subsequent disposal is a no-op. Throws if the active variant is not {variant.Name}.</summary>");
        sb.AppendLine($"public static void Take{variant.Name}(ref {model.TypeNameWithParameters} self, {outParams})");
        using (sb.Block())
        {
            sb.AppendLine($"if (self.{model.TagField} != Tags.{variant.Name}) ThrowInvalidCase(nameof({variant.Name}));");
            foreach (var param in variant.Parameters)
            {
                var field = variant.FieldName(param.Name);
                sb.AppendLine($"{CSharpIdentifiers.EscapeKeyword(param.Name)} = self.{field};");
            }
            sb.AppendLine("self = default;");
        }
    }

    static void EmitTryTake(SourceBuilder sb, UnionModel model, VariantModel variant)
    {
        if (variant.Parameters.Count == 1)
        {
            var param = variant.Parameters[0];
            var field = variant.FieldName(param.Name);
            sb.AppendLine($"/// <summary>If the active variant is {variant.Name}, removes ownership of its payload, writes it to <paramref name=\"value\"/>, sets <paramref name=\"self\"/> to default, and returns true.</summary>");
            sb.AppendLine($"public static bool TryTake{variant.Name}(ref {model.TypeNameWithParameters} self, out {param.TypeFullyQualified} value)");
            using (sb.Block())
            {
                sb.AppendLine($"if (self.{model.TagField} == Tags.{variant.Name})");
                using (sb.Block())
                {
                    sb.AppendLine($"value = self.{field};");
                    sb.AppendLine("self = default;");
                    sb.AppendLine("return true;");
                }
                sb.AppendLine();
                sb.AppendLine("value = default!;");
                sb.AppendLine("return false;");
            }
            return;
        }

        var outParams = string.Join(", ", variant.Parameters.Select(p =>
            $"out {p.TypeFullyQualified} {CSharpIdentifiers.EscapeKeyword(p.Name)}"));

        sb.AppendLine($"/// <summary>If the active variant is {variant.Name}, removes ownership of its payload, writes fields to the out parameters, sets <paramref name=\"self\"/> to default, and returns true.</summary>");
        sb.AppendLine($"public static bool TryTake{variant.Name}(ref {model.TypeNameWithParameters} self, {outParams})");
        using (sb.Block())
        {
            sb.AppendLine($"if (self.{model.TagField} == Tags.{variant.Name})");
            using (sb.Block())
            {
                foreach (var param in variant.Parameters)
                {
                    var field = variant.FieldName(param.Name);
                    sb.AppendLine($"{CSharpIdentifiers.EscapeKeyword(param.Name)} = self.{field};");
                }
                sb.AppendLine("self = default;");
                sb.AppendLine("return true;");
            }
            sb.AppendLine();
            foreach (var param in variant.Parameters)
            {
                sb.AppendLine($"{CSharpIdentifiers.EscapeKeyword(param.Name)} = default!;");
            }
            sb.AppendLine("return false;");
        }
    }

    static bool HasAnySyncField(VariantModel variant)
    {
        foreach (var p in variant.Parameters)
        {
            if (p.IsDisposable)
            {
                return true;
            }
        }
        return false;
    }
}
