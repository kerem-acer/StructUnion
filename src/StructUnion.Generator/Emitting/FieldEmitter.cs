using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;
using StructUnion.Generator.Parsing;

namespace StructUnion.Generator.Emitting;

static class FieldEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        sb.AppendLine("public enum Tags : byte");
        using (sb.Block())
        {
            sb.AppendLine("Default = 0,");
            foreach (var variant in model.Variants)
            {
                sb.AppendLine($"{variant.Name} = {variant.Tag},");
            }
        }

        sb.AppendLine();

        if (model.Layout == LayoutStrategy.Explicit)
        {
            EmitExplicitFields(sb, model);
        }
        else
        {
            EmitAutoFields(sb, model);
        }
    }

    static void EmitExplicitFields(SourceBuilder sb, UnionModel model)
    {
        sb.AppendLine("[global::System.Runtime.InteropServices.FieldOffset(0)]");
        sb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine("private readonly Tags _tag;");

        // Common fields
        for (var i = 0; i < model.CommonFields.Count; i++)
        {
            var field = model.CommonFields[i];
            var offset = LayoutCalculator.ComputeCommonFieldOffset(
                model.CommonFields.AsImmutableArray(), i);
            sb.AppendLine();
            sb.AppendLine($"[global::System.Runtime.InteropServices.FieldOffset({offset})]");
            sb.AppendLine($"public readonly {field.TypeFullyQualified} {field.Name};");
        }

        // Variant fields (two-zone: ref fields overlap in ref zone, value fields in value zone)
        foreach (var variant in model.Variants)
        {
            for (var i = 0; i < variant.Parameters.Count; i++)
            {
                var param = variant.Parameters[i];
                var offset = LayoutCalculator.ComputeVariantFieldOffset(
                    variant, i, model.RefZoneOffset, model.ValueZoneOffset);
                sb.AppendLine();
                sb.AppendLine($"[global::System.Runtime.InteropServices.FieldOffset({offset})]");
                sb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
                sb.AppendLine($"private readonly {param.TypeFullyQualified} _{variant.Name.ToLowerInvariant()}_{param.Name};");
            }
        }
    }

    static void EmitAutoFields(SourceBuilder sb, UnionModel model)
    {
        sb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine("private readonly Tags _tag;");

        foreach (var field in model.CommonFields)
        {
            sb.AppendLine($"public readonly {field.TypeFullyQualified} {field.Name};");
        }

        foreach (var variant in model.Variants)
        {
            foreach (var param in variant.Parameters)
            {
                sb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
                sb.AppendLine($"private readonly {param.TypeFullyQualified} _{variant.Name.ToLowerInvariant()}_{param.Name};");
            }
        }
    }
}
