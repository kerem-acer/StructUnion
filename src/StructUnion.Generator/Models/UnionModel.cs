using StructUnion.Generator.Infrastructure;

namespace StructUnion.Generator.Models;

enum GenerationMode
{
    /// <summary>Augment user's readonly partial struct with static partial methods.</summary>
    PartialStruct,

    /// <summary>Generate entire struct from record/class template.</summary>
    RecordTemplate
}

readonly record struct UnionModel(
    string Namespace,
    EquatableArray<string> ContainingTypes,
    string Accessibility,
    string Name,
    EquatableArray<TypeParameterModel> TypeParameters,
    EquatableArray<VariantModel> Variants,
    EquatableArray<FieldModel> CommonFields,
    LayoutStrategy Layout,
    bool EnableImplicitConversions,
    int RefZoneOffset,
    int ValueZoneOffset,
    int TotalSize,
    int StructAlignment,
    GenerationMode Mode,
    string TagPropertyName,
    bool NestedAccessors,
    bool GenerateDispose,
    bool NativeUnion,
    bool ImplementIUnion,
    string TemplateTypeName = "",
    string TemplateTypeKeyword = "")
{
    public bool HasCommonFields => CommonFields.Count > 0;

    public string TagField => "_tag";

    /// <summary>
    /// True if the nested <c>Cases</c> class is emitted. Native union mode needs it for its case
    /// types, so it is emitted there even when <see cref="NestedAccessors"/> is off.
    /// </summary>
    public bool EmitCases => NestedAccessors || NativeUnion;

    /// <summary>True if any variant carries no fields (so its case struct is empty).</summary>
    public bool HasAnyZeroParameterVariant
    {
        get
        {
            foreach (var v in Variants)
            {
                if (v.Parameters.Count == 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>True if any variant carries a field whose type implements IDisposable or IAsyncDisposable.</summary>
    public bool HasAnyDisposable
    {
        get
        {
            foreach (var v in Variants)
            {
                foreach (var p in v.Parameters)
                {
                    if (p.IsDisposable || p.IsAsyncDisposable)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>True if any variant carries a field whose type implements IDisposable (sync).</summary>
    public bool HasAnySyncDisposable
    {
        get
        {
            foreach (var v in Variants)
            {
                foreach (var p in v.Parameters)
                {
                    if (p.IsDisposable)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>True if any variant carries a field whose type implements IAsyncDisposable.</summary>
    public bool HasAnyAsyncDisposable
    {
        get
        {
            foreach (var v in Variants)
            {
                foreach (var p in v.Parameters)
                {
                    if (p.IsAsyncDisposable)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>True if the named variant carries any disposable (sync or async) field.</summary>
    public static bool VariantNeedsDisposal(VariantModel variant)
    {
        foreach (var p in variant.Parameters)
        {
            if (p.IsDisposable || p.IsAsyncDisposable)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Pre-computed hint name for AddSource.</summary>
    public string FullHintName { get; } = BuildFullHintName(Namespace, ContainingTypes, Name);

    /// <summary>Pre-computed type name with generic parameters for declarations.</summary>
    public string TypeNameWithParameters { get; } = BuildTypeNameWithParameters(Name, TypeParameters);

    /// <summary>
    /// Pre-computed <c>global::</c>-qualified name including namespace, containing types and
    /// generic arguments — e.g. <c>global::MyApp.Outer&lt;T&gt;.Shape</c>. Used where generated
    /// code must name the union itself and cannot rely on simple-name lookup.
    /// </summary>
    public string FullyQualifiedName { get; } =
        BuildFullyQualifiedName(Namespace, ContainingTypes, Name, TypeParameters);

    static string BuildFullHintName(string ns, EquatableArray<string> containingTypes, string name)
    {
        var prefix = ns.Length > 0 ? $"{ns}." : "";
        foreach (var ct in containingTypes)
        {
            var ctName = ct.Substring(ct.LastIndexOf(' ') + 1);
            var genericIdx = ctName.IndexOf('<');
            if (genericIdx >= 0)
            {
                ctName = ctName.Substring(0, genericIdx);
            }

            prefix += $"{ctName}.";
        }

        return $"{prefix}{name}";
    }

    static string BuildFullyQualifiedName(
        string ns,
        EquatableArray<string> containingTypes,
        string name,
        EquatableArray<TypeParameterModel> typeParameters)
    {
        var prefix = ns.Length > 0 ? $"global::{ns}." : "global::";
        foreach (var ct in containingTypes)
        {
            // Entries look like "partial class Outer<T>"; take everything after the last keyword.
            // Unlike the hint name, the generic arguments are kept — they are part of the type name.
            prefix += $"{ct.Substring(ct.LastIndexOf(' ') + 1)}.";
        }

        return $"{prefix}{BuildTypeNameWithParameters(name, typeParameters)}";
    }

    static string BuildTypeNameWithParameters(string name, EquatableArray<TypeParameterModel> typeParameters)
    {
        if (typeParameters.Count == 0)
        {
            return name;
        }

        var names = new string[typeParameters.Count];
        for (var i = 0; i < typeParameters.Count; i++)
        {
            names[i] = typeParameters[i].Name;
        }

        return $"{name}<{string.Join(", ", names)}>";
    }
}
