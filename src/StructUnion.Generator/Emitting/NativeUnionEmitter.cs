using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Emitting;

/// <summary>
/// Emits the C# 15 union member pattern when the union opts in via
/// <c>[StructUnion(NativeUnion = true)]</c>, making the generated struct a union type as far as
/// the compiler is concerned.
/// </summary>
/// <remarks>
/// <para>Members go on a nested <c>IUnionMembers</c> provider interface rather than on the struct
/// itself. The compiler looks union members up on the provider and never inspects how they are
/// implemented, so implementing them explicitly keeps <c>Value</c>, <c>HasValue</c> and
/// <c>TryGetValue</c> off the public surface — where they would otherwise collide with a variant
/// or common field named <c>Value</c> — and avoids adding public single-parameter constructors to
/// a type whose creation story is factory methods.</para>
/// <para>Explicit implementation costs nothing at runtime: a readonly struct receiver calling an
/// interface method compiles to <c>constrained. callvirt</c>, with no boxing and no defensive
/// copy. Pattern matching therefore goes through <c>TryGetValue</c> and never touches the boxing
/// <c>Value</c> path.</para>
/// </remarks>
static class NativeUnionEmitter
{
    const string Inline = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
    const string NeverBrowsable = "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]";
    const string IUnion = "global::System.Runtime.CompilerServices.IUnion";

    /// <summary>Returns the backing field holding the cached box for a zero-parameter variant.</summary>
    static string BoxedField(VariantModel variant) => $"s_boxed_{variant.NameLower}";

    /// <summary>
    /// Caches the box for each zero-parameter variant, so the <c>Value</c> fallback path is
    /// allocation-free for them. Static fields need no <c>[FieldOffset]</c> in an explicit layout.
    /// </summary>
    public static void EmitBoxedCaseFields(SourceBuilder sb, UnionModel model)
    {
        if (!model.NativeUnion || !model.HasAnyZeroParameterVariant)
        {
            return;
        }

        foreach (var variant in model.Variants)
        {
            if (variant.Parameters.Count > 0)
            {
                continue;
            }

            sb.AppendLine(NeverBrowsable);
            sb.AppendLine($"private static readonly object {BoxedField(variant)} = new Cases.{variant.Name}();");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Emits the nested provider interface. The static <c>Create</c> methods are what establish
    /// the union's case types; <c>TryGetValue</c> and <c>HasValue</c> are the optional non-boxing
    /// access pattern the compiler prefers over reading <c>Value</c>.
    /// </summary>
    public static void EmitMemberProviderInterface(SourceBuilder sb, UnionModel model)
    {
        if (!model.NativeUnion)
        {
            return;
        }

        // No cref here: a generic union's name would need the `Option{T}` doc-comment spelling,
        // and the payoff is nil on an interface that is hidden from IntelliSense anyway.
        sb.AppendLine("/// <summary>C# 15 union member provider for the containing union. Generated; do not use directly.</summary>");
        sb.AppendLine(NeverBrowsable);
        sb.AppendLine("public interface IUnionMembers");
        using (sb.Block())
        {
            sb.AppendLine("object? Value { get; }");
            sb.AppendLine();
            sb.AppendLine("bool HasValue { get; }");

            foreach (var variant in model.Variants)
            {
                sb.AppendLine();
                sb.AppendLine($"bool TryGetValue(out Cases.{variant.Name} value);");
            }

            foreach (var variant in model.Variants)
            {
                var args = string.Join(", ", variant.Parameters.Select(p =>
                    $"value.{CSharpIdentifiers.ToPascalCase(p.Name)}"));

                sb.AppendLine();
                sb.AppendLine(Inline);
                sb.AppendLine($"public static {model.TypeNameWithParameters} Create(Cases.{variant.Name} value) => {model.FullyQualifiedName}.{variant.Name}({args});");
            }
        }

        sb.AppendLine();
    }

    public static void EmitExplicitImplementations(SourceBuilder sb, UnionModel model)
    {
        if (!model.NativeUnion)
        {
            return;
        }

        var provider = $"{model.FullyQualifiedName}.IUnionMembers";

        EmitGetUnionValue(sb, model);

        sb.AppendLine($"object? {provider}.Value");
        using (sb.Block())
        {
            sb.AppendLine(Inline);
            sb.AppendLine("get => GetUnionValue();");
        }

        sb.AppendLine();

        if (model.ImplementIUnion)
        {
            sb.AppendLine($"object? {IUnion}.Value");
            using (sb.Block())
            {
                sb.AppendLine(Inline);
                sb.AppendLine("get => GetUnionValue();");
            }

            sb.AppendLine();
        }

        sb.AppendLine($"bool {provider}.HasValue");
        using (sb.Block())
        {
            sb.AppendLine(Inline);
            sb.AppendLine($"get => {model.TagField} != Tags.Default;");
        }

        sb.AppendLine();

        foreach (var variant in model.Variants)
        {
            EmitTryGetValue(sb, model, variant, provider);
        }
    }

    /// <summary>
    /// The one boxing site, shared by both interfaces so <c>this</c> is never cast to an interface
    /// (which would box). A switch statement rather than an expression: the arms are different
    /// struct types and have no best common type.
    /// </summary>
    static void EmitGetUnionValue(SourceBuilder sb, UnionModel model)
    {
        sb.AppendLine(Inline);
        sb.AppendLine("private object? GetUnionValue()");
        using (sb.Block())
        {
            sb.AppendLine($"switch ({model.TagField})");
            using (sb.Block())
            {
                foreach (var variant in model.Variants)
                {
                    if (variant.Parameters.Count == 0)
                    {
                        sb.AppendLine($"case Tags.{variant.Name}: return {BoxedField(variant)};");
                        continue;
                    }

                    var args = string.Join(", ", variant.Parameters.Select(p => variant.FieldName(p.Name)));
                    sb.AppendLine($"case Tags.{variant.Name}: return new Cases.{variant.Name}({args});");
                }

                // default(T) must produce a null Value — the compiler relies on it for soundness.
                sb.AppendLine("default: return null;");
            }
        }

        sb.AppendLine();
    }

    static void EmitTryGetValue(SourceBuilder sb, UnionModel model, VariantModel variant, string provider)
    {
        sb.AppendLine(Inline);
        sb.AppendLine($"bool {provider}.TryGetValue(out Cases.{variant.Name} value)");
        using (sb.Block())
        {
            sb.AppendLine($"if ({model.TagField} == Tags.{variant.Name})");
            using (sb.Block())
            {
                if (variant.Parameters.Count == 0)
                {
                    sb.AppendLine("value = default;");
                }
                else
                {
                    var args = string.Join(", ", variant.Parameters.Select(p => variant.FieldName(p.Name)));
                    sb.AppendLine($"value = new Cases.{variant.Name}({args});");
                }

                sb.AppendLine("return true;");
            }

            sb.AppendLine();
            sb.AppendLine("value = default;");
            sb.AppendLine("return false;");
        }

        sb.AppendLine();
    }
}
