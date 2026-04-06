using Microsoft.CodeAnalysis;

namespace StructUnion.Generator.Parsing;

/// <summary>
/// Computes size and alignment of types for explicit struct layout.
/// Returns -1 only for unknowable types (unconstrained generic type parameters).
/// Reference types are 8 bytes (pointer size on 64-bit).
/// </summary>
static class TypeClassifier
{
    public const int ReferenceSize = 8;
    public const int ReferenceAlignment = 8;

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

    /// <summary>
    /// Returns (fqn, size, alignment) in a single pass, calling ToDisplayString only once.
    /// Use this for top-level field classification to avoid redundant Roslyn calls.
    /// </summary>
    public static (string Fqn, int Size, int Alignment) Classify(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), -1, -1);
        }

        if (!type.IsValueType)
        {
            return (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), ReferenceSize, ReferenceAlignment);
        }

        var sizeFromSpecial = GetSizeFromSpecialType(type.SpecialType);
        if (sizeFromSpecial > 0)
        {
            return (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                sizeFromSpecial, GetAlignmentFromSpecialType(type.SpecialType));
        }

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (WellKnownTypes.TryGetValue(fqn, out var known))
        {
            return (fqn, known.Size, known.Alignment);
        }

        var (size, alignment) = TryComputeStruct(type);
        return (fqn, size, alignment);
    }

    public static int GetSize(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return -1;
        }

        if (!type.IsValueType)
        {
            return ReferenceSize;
        }

        var fromSpecial = GetSizeFromSpecialType(type.SpecialType);
        if (fromSpecial > 0)
        {
            return fromSpecial;
        }

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (WellKnownTypes.TryGetValue(fqn, out var known))
        {
            return known.Size;
        }

        return TryComputeStruct(type).Size;
    }

    public static int GetAlignment(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return -1;
        }

        if (!type.IsValueType)
        {
            return ReferenceAlignment;
        }

        var fromSpecial = GetAlignmentFromSpecialType(type.SpecialType);
        if (fromSpecial > 0)
        {
            return fromSpecial;
        }

        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (WellKnownTypes.TryGetValue(fqn, out var known))
        {
            return known.Alignment;
        }

        return TryComputeStruct(type).Alignment;
    }

    public static int Align(int offset, int alignment) =>
        alignment <= 1 ? offset : (offset + alignment - 1) & ~(alignment - 1);

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
    /// Computes both size and alignment for a struct type in a single pass,
    /// avoiding duplicate GetSize/GetAlignment calls on each field.
    /// </summary>
    static (int Size, int Alignment) TryComputeStruct(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            var underlying = ((INamedTypeSymbol)type).EnumUnderlyingType!;
            return (GetSize(underlying), GetAlignment(underlying));
        }

        if (!type.IsValueType || type is not INamedTypeSymbol named)
        {
            return (-1, -1);
        }

        var totalSize = 0;
        var maxAlignment = 1;
        foreach (var member in named.GetMembers())
        {
            if (member is not IFieldSymbol { IsStatic: false, IsConst: false } field)
            {
                continue;
            }

            var fieldSize = GetSize(field.Type);
            var fieldAlignment = GetAlignment(field.Type);
            if (fieldSize < 0 || fieldAlignment < 0)
            {
                return (-1, -1);
            }

            totalSize = Align(totalSize, fieldAlignment);
            totalSize += fieldSize;
            maxAlignment = Math.Max(maxAlignment, fieldAlignment);
        }

        return (Align(totalSize, maxAlignment), maxAlignment);
    }
}
