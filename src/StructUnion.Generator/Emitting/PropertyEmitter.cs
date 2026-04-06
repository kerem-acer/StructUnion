using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

static class PropertyEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        var tag = model.TagField;

        sb.AppendLine("/// <summary>Gets the tag value identifying which variant is active.</summary>");
        sb.AppendLine($"public Tags {model.TagPropertyName} => {tag};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Returns true if this is a default-constructed instance with no active variant.</summary>");
        sb.AppendLine($"public bool IsDefault => {tag} == Tags.Default;");
        sb.AppendLine();

        foreach (var variant in model.Variants)
        {
            sb.AppendLine($"public bool Is{variant.Name} => {tag} == Tags.{variant.Name};");
        }

        sb.AppendLine();

        if (model.NestedAccessors)
        {
            EmitNestedAccessors(sb, model);
        }
        else
        {
            EmitFlatAccessors(sb, model);
        }

        sb.AppendLine();

        foreach (var variant in model.Variants)
        {
            if (model.NestedAccessors)
            {
                EmitTryGetNested(sb, model, variant);
            }
            else
            {
                EmitTryGet(sb, model, variant);
            }
        }
    }

    static void EmitFlatAccessors(SourceBuilder sb, UnionModel model)
    {
        var tag = model.TagField;

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
                        sb.AppendLine($"if ({tag} != Tags.{variant.Name}) ThrowInvalidCase(nameof({variant.Name}));");
                        sb.AppendLine($"return {field};");
                    }
                }
            }
        }
    }

    static void EmitNestedAccessors(SourceBuilder sb, UnionModel model)
    {
        var tag = model.TagField;

        foreach (var variant in model.Variants)
        {
            if (variant.Parameters.Count == 0)
            {
                continue;
            }

            var args = string.Join(", ", variant.Parameters.Select(p =>
                model.VariantField(variant.Name, p.Name)));

            sb.AppendLine($"public Cases.{variant.Name} As{variant.Name}");
            using (sb.Block())
            {
                sb.AppendLine("get");
                using (sb.Block())
                {
                    sb.AppendLine($"if ({tag} != Tags.{variant.Name}) ThrowInvalidCase(nameof({variant.Name}));");
                    sb.AppendLine($"return new Cases.{variant.Name}({args});");
                }
            }
        }
    }

    static void EmitTryGet(SourceBuilder sb, UnionModel model, VariantModel variant)
    {
        var tag = model.TagField;

        if (variant.Parameters.Count == 0)
        {
            sb.AppendLine($"public bool TryGet{variant.Name}() => {tag} == Tags.{variant.Name};");
            sb.AppendLine();
            return;
        }

        var outParams = string.Join(", ", variant.Parameters.Select(p =>
            $"out {p.TypeFullyQualified} {CSharpIdentifiers.EscapeKeyword(p.Name)}"));

        sb.AppendLine($"public bool TryGet{variant.Name}({outParams})");
        using (sb.Block())
        {
            sb.AppendLine($"if ({tag} == Tags.{variant.Name})");
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

    static void EmitTryGetNested(SourceBuilder sb, UnionModel model, VariantModel variant)
    {
        var tag = model.TagField;

        if (variant.Parameters.Count == 0)
        {
            sb.AppendLine($"public bool TryGet{variant.Name}() => {tag} == Tags.{variant.Name};");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"public bool TryGet{variant.Name}(out Cases.{variant.Name} data)");
        using (sb.Block())
        {
            var args = string.Join(", ", variant.Parameters.Select(p =>
                model.VariantField(variant.Name, p.Name)));

            sb.AppendLine($"if ({tag} == Tags.{variant.Name})");
            using (sb.Block())
            {
                sb.AppendLine($"data = new Cases.{variant.Name}({args});");
                sb.AppendLine("return true;");
            }
            sb.AppendLine();
            sb.AppendLine("data = default;");
            sb.AppendLine("return false;");
        }
        sb.AppendLine();
    }
}
