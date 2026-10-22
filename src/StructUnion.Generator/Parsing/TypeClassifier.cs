using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.Generator.Parsing;

/// <summary>
/// Computes size and alignment of types for explicit struct layout.
/// Returns -1 for unknowable types: unconstrained generic type parameters, and types the
/// compilation cannot see yet. Reference types are 8 bytes (pointer size on 64-bit).
/// </summary>
static class TypeClassifier
{
    public const int ReferenceSize = 8;
    public const int ReferenceAlignment = 8;

    /// <summary>
    /// How deep to follow struct fields. A struct cannot contain itself, so only a compilation that
    /// is already in error gets this deep; the cap just keeps the generator off the stack's end.
    /// </summary>
    const int MaxDepth = 32;

    static readonly string StructUnionAttributeName = typeof(StructUnionAttribute).FullName;

    /// <summary>
    /// Well-known value types whose internal fields may not be visible to Roslyn.
    /// (name, size, alignment)
    /// </summary>
    static readonly Dictionary<string, (int Size, int Alignment)> WellKnownTypes = new()
    {
        ["global::System.Guid"] = (16, 4),
        ["global::System.DateTime"] = (8, 8),
        ["global::System.DateTimeOffset"] = (16, 8),
        ["global::System.TimeSpan"] = (8, 8),
        ["global::System.DateOnly"] = (4, 4),
        ["global::System.TimeOnly"] = (8, 8),
        ["global::System.Half"] = (2, 2),
        ["global::System.Int128"] = (16, 8),
        ["global::System.UInt128"] = (16, 8),
    };

    static readonly TypeShape Unknown = new(-1, -1, false);

    /// <summary>
    /// Returns (fqn, size, alignment, holds references) in a single pass, calling ToDisplayString only once.
    /// Use this for top-level field classification to avoid redundant Roslyn calls.
    /// </summary>
    /// <remarks>
    /// <c>HoldsReferences</c> can be true where Roslyn reports <c>IsUnmanagedType</c>: a union declared
    /// in this compilation has no fields yet as far as Roslyn knows, whatever its cases carry.
    /// </remarks>
    public static (string Fqn, int Size, int Alignment, bool HoldsReferences) Classify(ITypeSymbol type)
    {
        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var shape = ComputeShape(type, fqn, 0);
        return (fqn, shape.Size, shape.Alignment, shape.HoldsReferences);
    }

    public static int GetSize(ITypeSymbol type) => ComputeShape(type, null, 0).Size;

    public static int GetAlignment(ITypeSymbol type) => ComputeShape(type, null, 0).Alignment;

    public static int Align(int offset, int alignment) =>
        alignment <= 1 ? offset : (offset + alignment - 1) & ~(alignment - 1);

    static TypeShape ComputeShape(ITypeSymbol type, string? fqn, int depth)
    {
        // An error type is one the compilation cannot see yet, such as the struct a [StructUnion]
        // record template is about to generate. It could be anything, so it is not guessed at.
        if (type.TypeKind is TypeKind.TypeParameter or TypeKind.Error || depth > MaxDepth)
        {
            return Unknown;
        }

        if (!type.IsValueType)
        {
            return new TypeShape(ReferenceSize, ReferenceAlignment, HoldsReferences: true);
        }

        var sizeFromSpecial = GetSizeFromSpecialType(type.SpecialType);
        if (sizeFromSpecial > 0)
        {
            return new TypeShape(sizeFromSpecial, GetAlignmentFromSpecialType(type.SpecialType), HoldsReferences: false);
        }

        fqn ??= type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (WellKnownTypes.TryGetValue(fqn, out var known))
        {
            return new TypeShape(known.Size, known.Alignment, HoldsReferences: false);
        }

        return ComputeStructShape(type, depth);
    }

    static int GetSizeFromSpecialType(SpecialType st) => st switch
    {
        SpecialType.System_Boolean => 1,
        SpecialType.System_Byte => 1,
        SpecialType.System_SByte => 1,
        SpecialType.System_Int16 => 2,
        SpecialType.System_UInt16 => 2,
        SpecialType.System_Char => 2,
        SpecialType.System_Int32 => 4,
        SpecialType.System_UInt32 => 4,
        SpecialType.System_Single => 4,
        SpecialType.System_Int64 => 8,
        SpecialType.System_UInt64 => 8,
        SpecialType.System_Double => 8,
        SpecialType.System_IntPtr => 8,
        SpecialType.System_UIntPtr => 8,
        SpecialType.System_Decimal => 16,
        _ => 0
    };

    static int GetAlignmentFromSpecialType(SpecialType st) => st switch
    {
        SpecialType.System_Boolean => 1,
        SpecialType.System_Byte => 1,
        SpecialType.System_SByte => 1,
        SpecialType.System_Int16 => 2,
        SpecialType.System_UInt16 => 2,
        SpecialType.System_Char => 2,
        SpecialType.System_Int32 => 4,
        SpecialType.System_UInt32 => 4,
        SpecialType.System_Single => 4,
        SpecialType.System_Int64 => 8,
        SpecialType.System_UInt64 => 8,
        SpecialType.System_Double => 8,
        SpecialType.System_IntPtr => 8,
        SpecialType.System_UIntPtr => 8,
        SpecialType.System_Decimal => 8,
        _ => 0
    };

    /// <summary>
    /// Computes size, alignment and whether it holds references for a struct type in a single
    /// pass over its fields.
    /// </summary>
    static TypeShape ComputeStructShape(ITypeSymbol type, int depth)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            var underlying = ((INamedTypeSymbol)type).EnumUnderlyingType!;
            return ComputeShape(underlying, null, depth + 1);
        }

        if (!type.IsValueType || type is not INamedTypeSymbol named)
        {
            return Unknown;
        }

        // Generators never see their own output, so a union declared in this compilation has no
        // fields yet: adding them up would make it an empty, unmanaged struct.
        if (IsUnionInSource(named))
        {
            return ComputeSourceUnionShape(named, depth);
        }

        var fields = GetInstanceFields(named);
        if (TryComputeCompiledUnionShape(named, fields, depth, out var unionShape))
        {
            return unionShape;
        }

        var totalSize = 0;
        var maxAlignment = 1;
        var holdsReferences = false;
        foreach (var field in fields)
        {
            var shape = ComputeShape(field.Type, null, depth + 1);
            if (shape.Size < 0 || shape.Alignment < 0)
            {
                return Unknown;
            }

            totalSize = Align(totalSize, shape.Alignment);
            totalSize += shape.Size;
            maxAlignment = Math.Max(maxAlignment, shape.Alignment);
            holdsReferences |= shape.HoldsReferences;
        }

        return new TypeShape(Align(totalSize, maxAlignment), maxAlignment, holdsReferences);
    }

    static bool IsUnionInSource(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Struct || type.DeclaringSyntaxReferences.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var attribute in type.OriginalDefinition.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == StructUnionAttributeName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sizes a union declared in this compilation from its cases, planned the way its own
    /// generation will plan them.
    /// </summary>
    static TypeShape ComputeSourceUnionShape(INamedTypeSymbol union, int depth)
    {
        var variants = new List<VariantModel>();
        foreach (var member in union.GetMembers())
        {
            // Members of a constructed generic union are substituted, and only the original
            // definition knows it is a partial definition.
            if (member is not IMethodSymbol { IsStatic: true } method
                || !method.OriginalDefinition.IsPartialDefinition
                || !SymbolEqualityComparer.Default.Equals(method.ReturnType, union))
            {
                continue;
            }

            var parameters = ImmutableArray.CreateBuilder<FieldModel>(method.Parameters.Length);
            foreach (var parameter in method.Parameters)
            {
                parameters.Add(CreateLayoutField(parameter.Name, parameter.Type, depth + 1));
            }

            variants.Add(new VariantModel(method.Name, parameters.MoveToImmutable().ToEquatableArray(), 0));
        }

        return FromPlan(LayoutCalculator.Plan(variants, [], union.IsInGenericContext()));
    }

    /// <summary>
    /// Sizes a union generated into a referenced assembly.
    /// </summary>
    /// <remarks>
    /// Its fields are visible but their <c>FieldOffset</c>s are not — Roslyn's public API does not
    /// expose explicit layout — so adding the fields up counts every case at once. This rebuilds the
    /// cases from the generated field names and plans them again.
    /// </remarks>
    static bool TryComputeCompiledUnionShape(
        INamedTypeSymbol type,
        IReadOnlyList<IFieldSymbol> fields,
        int depth,
        out TypeShape shape)
    {
        shape = default;

        // Every generated union declares `enum Tags : byte` and keeps its tag in `_tag`, either on
        // the union itself or, under Hybrid layout, on its nested storage struct.
        if (!fields.Any(field => field.Name is "_tag" or UnionModel.StorageField))
        {
            return false;
        }

        var tags = type.GetTypeMembers("Tags").FirstOrDefault(t => t.TypeKind == TypeKind.Enum);
        if (tags is null)
        {
            return false;
        }

        var hasTag = false;
        var caseFields = new List<IFieldSymbol>();
        var commonFields = ImmutableArray.CreateBuilder<FieldModel>();
        foreach (var field in fields)
        {
            if (field.Name == "_tag")
            {
                hasTag = SymbolEqualityComparer.Default.Equals(field.Type, tags);
            }
            else if (field.Name == UnionModel.StorageField && field.Type is INamedTypeSymbol storage)
            {
                foreach (var stored in GetInstanceFields(storage))
                {
                    if (stored.Name == "_tag")
                    {
                        hasTag = SymbolEqualityComparer.Default.Equals(stored.Type, tags);
                    }
                    else
                    {
                        caseFields.Add(stored);
                    }
                }
            }
            else if (field.DeclaredAccessibility == Accessibility.Public)
            {
                commonFields.Add(CreateLayoutField(field.Name, field.Type, depth + 1));
            }
            else
            {
                caseFields.Add(field);
            }
        }

        if (!hasTag)
        {
            return false;
        }

        var caseNames = new List<string>();
        foreach (var member in tags.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true, Name: not "Default" } tag)
            {
                caseNames.Add(tag.Name);
            }
        }

        // Case fields are named `_{case}_{parameter}`, the case lowercased. Longer case names go
        // first, so `_a_b_x` lands in case `A_b` rather than `A`.
        var longestFirst = caseNames.OrderByDescending(name => name.Length).ToList();
        var parameters = caseNames.ToDictionary(name => name, _ => ImmutableArray.CreateBuilder<FieldModel>());
        foreach (var field in caseFields)
        {
            var owner = longestFirst.FirstOrDefault(name =>
                field.Name.StartsWith($"_{name.ToLowerInvariant()}_", StringComparison.Ordinal));
            if (owner is null)
            {
                return false;
            }

            parameters[owner].Add(CreateLayoutField(field.Name, field.Type, depth + 1));
        }

        var variants = caseNames
            .Select(name => new VariantModel(name, parameters[name].ToImmutable().ToEquatableArray(), 0))
            .ToList();

        shape = FromPlan(LayoutCalculator.Plan(variants, commonFields.ToImmutable(), type.IsInGenericContext()));
        return true;
    }

    /// <summary>
    /// A field carrying only what layout planning reads.
    /// </summary>
    static FieldModel CreateLayoutField(string name, ITypeSymbol type, int depth)
    {
        var shape = ComputeShape(type, null, depth);
        return new FieldModel(
            name,
            TypeFullyQualified: "",
            Accessibility: "",
            type.IsValueType,
            type.IsUnmanagedType && !shape.HoldsReferences,
            shape.Size,
            shape.Alignment,
            IsRefLike: type.IsRefLikeType);
    }

    static TypeShape FromPlan(LayoutPlan plan) =>
        new(plan.EstimatedSize, plan.EstimatedAlignment, plan.HoldsReferences);

    static List<IFieldSymbol> GetInstanceFields(INamedTypeSymbol type)
    {
        var fields = new List<IFieldSymbol>();
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol { IsStatic: false, IsConst: false } field)
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    readonly record struct TypeShape(int Size, int Alignment, bool HoldsReferences);
}
