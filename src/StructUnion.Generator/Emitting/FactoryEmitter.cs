using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

static class FactoryEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        sb.AppendLine("[global::System.Obsolete(\"Use factory methods instead.\", true)]");
        sb.AppendLine($"public {model.Name}() {{ }}");
        sb.AppendLine();

        foreach (var variant in model.Variants)
        {
            EmitFactoryMethod(sb, model, variant);
            sb.AppendLine();
        }
    }

    static void EmitFactoryMethod(SourceBuilder sb, UnionModel model, VariantModel variant)
    {
        var allParams = new List<string>();

        if (model.Mode == GenerationMode.RecordTemplate)
        {
            foreach (var field in model.CommonFields)
            {
                allParams.Add($"{field.TypeFullyQualified} {CSharpIdentifiers.ToCamelCase(field.Name)}");
            }
        }

        foreach (var param in variant.Parameters)
        {
            allParams.Add($"{param.TypeFullyQualified} {CSharpIdentifiers.ToCamelCase(param.Name)}");
        }

        var paramList = string.Join(", ", allParams);
        var partialKeyword = model.Mode == GenerationMode.PartialStruct ? "partial " : "";

        sb.AppendLine($"public static {partialKeyword}{model.TypeNameWithParameters} {variant.Name}({paramList})");
        using (sb.Block())
        {
            sb.AppendLine($"var result = default({model.TypeNameWithParameters});");
            sb.AppendLine($"global::System.Runtime.CompilerServices.Unsafe.AsRef(in result._tag) = Tags.{variant.Name};");

            if (model.Mode == GenerationMode.RecordTemplate)
            {
                foreach (var field in model.CommonFields)
                {
                    sb.AppendLine($"global::System.Runtime.CompilerServices.Unsafe.AsRef(in result.{field.Name}) = {CSharpIdentifiers.ToCamelCase(field.Name)};");
                }
            }

            foreach (var param in variant.Parameters)
            {
                var field = model.VariantField(variant.Name, param.Name);
                sb.AppendLine($"global::System.Runtime.CompilerServices.Unsafe.AsRef(in result.{field}) = {CSharpIdentifiers.ToCamelCase(param.Name)};");
            }

            sb.AppendLine("return result;");
        }
    }
}
