using StructUnion.Generator.Models;

namespace StructUnion.Generator.Parsing;

/// <summary>
/// Computes layout strategy and field offsets for a union.
///
/// Two-zone explicit layout for mixed ref/value unions:
///   [tag] [common fields] [ref zone (overlapping)] [value zone (overlapping)]
///
/// CLR allows ref-ref overlap and value-value overlap at the same FieldOffset,
/// but NOT ref-value. A field that fits neither zone — a value type holding references, or a type
/// whose size is unknown — cannot overlap anything; under <see cref="LayoutStrategy.Hybrid"/> the
/// zones move into a nested explicit-layout struct and the runtime places such fields beside it.
/// </summary>
static class LayoutCalculator
{
    /// <summary>
    /// Chooses the strategy, offsets and sizes for a union in one go.
    /// </summary>
    public static LayoutPlan Plan(
        IReadOnlyList<VariantModel> variants,
        IReadOnlyList<FieldModel> commonFields,
        bool isGeneric)
    {
        var strategy = DetermineStrategy(variants, commonFields, isGeneric);
        if (strategy == LayoutStrategy.Auto)
        {
            var (autoSize, autoAlignment) = EstimateSequential(strategy, variants, commonFields, 0, 0);
            return new LayoutPlan(strategy, 0, 0, 0, 0, autoSize, autoAlignment, HoldsReferences(variants, commonFields));
        }

        // Under Hybrid the common fields sit in the union itself, beside the storage struct.
        var storageCommonFields = strategy == LayoutStrategy.Explicit ? commonFields : [];
        var (refZoneOffset, valueZoneOffset) = ComputeZoneOffsets(storageCommonFields, variants);
        var (totalSize, structAlignment) = ComputeTotalSize(variants, storageCommonFields, refZoneOffset, valueZoneOffset);

        var (estimatedSize, estimatedAlignment) = strategy == LayoutStrategy.Explicit
            ? (totalSize, structAlignment)
            : EstimateSequential(strategy, variants, commonFields, totalSize, structAlignment);

        return new LayoutPlan(
            strategy,
            refZoneOffset,
            valueZoneOffset,
            totalSize,
            structAlignment,
            estimatedSize,
            estimatedAlignment,
            HoldsReferences(variants, commonFields));
    }

    public static LayoutStrategy DetermineStrategy(
        IReadOnlyList<VariantModel> variants,
        IReadOnlyList<FieldModel> commonFields,
        bool isGeneric)
    {
        // The CLR refuses explicit layout on any generic type, even one whose fields never mention a
        // type parameter — and a struct nested in a generic type is generic too.
        if (isGeneric)
        {
            return LayoutStrategy.Auto;
        }

        var anyRefLike = false;
        var anyNonOverlapping = false;
        var anyOverlappingVariantField = false;

        foreach (var field in commonFields)
        {
            anyRefLike |= field.IsRefLike;
            anyNonOverlapping |= !CanOverlap(field);
        }

        foreach (var variant in variants)
        {
            foreach (var param in variant.Parameters)
            {
                anyRefLike |= param.IsRefLike;
                if (CanOverlap(param))
                {
                    anyOverlappingVariantField = true;
                }
                else
                {
                    anyNonOverlapping = true;
                }
            }
        }

        // A byref has no representation at a fixed offset, and a ref struct union stays sequential.
        if (anyRefLike)
        {
            return LayoutStrategy.Auto;
        }

        if (!anyNonOverlapping)
        {
            return LayoutStrategy.Explicit;
        }

        // Nested storage only pays off when it holds case fields that can share memory.
        return anyOverlappingVariantField ? LayoutStrategy.Hybrid : LayoutStrategy.Auto;
    }

    /// <summary>
    /// True if the field can sit at an explicit <c>FieldOffset</c>, overlapping fields of other cases.
    /// </summary>
    /// <remarks>
    /// That takes a known size and one of two kinds: a reference, or an unmanaged value. A managed
    /// value type (a value type containing references, e.g. <c>ValueTuple&lt;string, int&gt;</c>) is
    /// neither — the runtime lays it out itself, so the generator cannot know which of its bytes are
    /// references. A ref-like type carries a byref, which has no representation at a fixed offset —
    /// and a <c>ref struct</c> whose own fields are all unmanaged reports <c>IsUnmanagedType == true</c>,
    /// so the managed-value-type test alone lets it through. The C# compiler accepts
    /// <c>LayoutKind.Explicit</c> on a ref struct without complaint, so nothing downstream catches
    /// the mistake.
    /// </remarks>
    public static bool CanOverlap(FieldModel field) =>
        field.Size >= 0
        && field.Alignment >= 0
        && !field.IsRefLike
        && !(field.IsValueType && !field.IsUnmanaged);

    /// <summary>
    /// Computes the starting offsets for the ref zone and value zone.
    /// When value fields fit in the padding gap before the ref zone, they are
    /// placed there to reduce struct size (valueZoneOffset &lt; refZoneOffset).
    /// </summary>
    public static (int RefZoneOffset, int ValueZoneOffset) ComputeZoneOffsets(
        IReadOnlyList<FieldModel> commonFields,
        IReadOnlyList<VariantModel> variants)
    {
        var offset = 1; // tag byte

        // Common fields placed sequentially after tag
        foreach (var field in commonFields)
        {
            if (field.Alignment > 0)
            {
                offset = TypeClassifier.Align(offset, field.Alignment);
            }

            offset += field.Size;
        }

        var gapStart = offset;

        // Scan for ref/value variant fields
        var hasRefFields = false;
        var hasValueFields = false;
        var maxValueAlignment = 1;

        foreach (var variant in variants)
        {
            foreach (var param in variant.Parameters)
            {
                if (!CanOverlap(param))
                {
                    continue;
                }

                if (IsRefField(param))
                {
                    hasRefFields = true;
                }
                else
                {
                    hasValueFields = true;
                    maxValueAlignment = Math.Max(maxValueAlignment, param.Alignment);
                }
            }
        }

        // Ref zone (8-byte aligned, each ref is 8 bytes)
        var refZoneOffset = hasRefFields
            ? TypeClassifier.Align(offset, TypeClassifier.ReferenceAlignment)
            : offset;

        var maxRefSlots = 0;
        if (hasRefFields)
        {
            foreach (var variant in variants)
            {
                maxRefSlots = Math.Max(maxRefSlots, CountRefFields(variant));
            }
        }

        var afterRefZone = refZoneOffset + maxRefSlots * TypeClassifier.ReferenceSize;

        if (!hasValueFields)
        {
            return (refZoneOffset, afterRefZone);
        }

        // Try to fit value fields in the gap before the ref zone
        if (hasRefFields && ValueFieldsFitInGap(variants, gapStart, refZoneOffset, maxValueAlignment))
        {
            var valueZoneOffset = TypeClassifier.Align(gapStart, maxValueAlignment);
            return (refZoneOffset, valueZoneOffset);
        }

        // Fallback: value zone after ref zone
        var valueZoneAfterRef = TypeClassifier.Align(afterRefZone, maxValueAlignment);
        return (refZoneOffset, valueZoneAfterRef);
    }

    static bool ValueFieldsFitInGap(
        IReadOnlyList<VariantModel> variants, int gapStart, int refZoneOffset, int maxValueAlignment)
    {
        // Check every variant's value fields fit within [gapStart, refZoneOffset)
        foreach (var variant in variants)
        {
            var pos = TypeClassifier.Align(gapStart, maxValueAlignment);
            foreach (var param in variant.Parameters)
            {
                if (!IsValueZoneField(param))
                {
                    continue;
                }

                pos = TypeClassifier.Align(pos, param.Alignment);
                pos += param.Size;
            }
            if (pos > refZoneOffset)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Computes the offset for a common field given its position.
    /// </summary>
    public static int ComputeCommonFieldOffset(IReadOnlyList<FieldModel> commonFields, int fieldIndex)
    {
        var offset = 1;

        for (var i = 0; i < fieldIndex; i++)
        {
            if (commonFields[i].Alignment > 0)
            {
                offset = TypeClassifier.Align(offset, commonFields[i].Alignment);
            }

            offset += commonFields[i].Size;
        }

        if (commonFields[fieldIndex].Alignment > 0)
        {
            offset = TypeClassifier.Align(offset, commonFields[fieldIndex].Alignment);
        }

        return offset;
    }

    /// <summary>
    /// Computes field offset for a variant parameter.
    /// Ref fields go to the ref zone, value fields go to the value zone; fields that cannot
    /// overlap are not in the explicit-layout struct and take no space in either.
    /// </summary>
    public static int ComputeVariantFieldOffset(
        VariantModel variant, int paramIndex, int refZoneOffset, int valueZoneOffset)
    {
        var param = variant.Parameters[paramIndex];

        if (IsRefField(param))
        {
            var refIndex = 0;
            for (var i = 0; i < paramIndex; i++)
            {
                if (CanOverlap(variant.Parameters[i]) && IsRefField(variant.Parameters[i]))
                {
                    refIndex++;
                }
            }

            return refZoneOffset + refIndex * TypeClassifier.ReferenceSize;
        }

        // Value field — lay out sequentially in value zone, skipping ref fields
        var offset = valueZoneOffset;
        for (var i = 0; i < paramIndex; i++)
        {
            if (!IsValueZoneField(variant.Parameters[i]))
            {
                continue;
            }

            offset = TypeClassifier.Align(offset, variant.Parameters[i].Alignment);
            offset += variant.Parameters[i].Size;
        }

        return TypeClassifier.Align(offset, param.Alignment);
    }

    /// <summary>
    /// Computes the total struct size and alignment for explicit layout unions.
    /// </summary>
    public static (int TotalSize, int StructAlignment) ComputeTotalSize(
        IReadOnlyList<VariantModel> variants,
        IReadOnlyList<FieldModel> commonFields,
        int refZoneOffset,
        int valueZoneOffset)
    {
        var maxRefSlots = 0;
        var hasRefFields = false;
        var structAlignment = 1;

        foreach (var variant in variants)
        {
            foreach (var param in variant.Parameters)
            {
                if (!CanOverlap(param))
                {
                    continue;
                }

                if (IsRefField(param))
                {
                    hasRefFields = true;
                }
                structAlignment = Math.Max(structAlignment, param.Alignment);
            }
            maxRefSlots = Math.Max(maxRefSlots, CountRefFields(variant));
        }
        foreach (var field in commonFields)
        {
            structAlignment = Math.Max(structAlignment, field.Alignment);
        }

        if (hasRefFields)
        {
            structAlignment = Math.Max(structAlignment, TypeClassifier.ReferenceAlignment);
        }

        var afterRefZone = refZoneOffset + maxRefSlots * TypeClassifier.ReferenceSize;

        var maxEnd = afterRefZone;
        foreach (var variant in variants)
        {
            var endPos = valueZoneOffset;
            foreach (var param in variant.Parameters)
            {
                if (!IsValueZoneField(param))
                {
                    continue;
                }

                endPos = TypeClassifier.Align(endPos, param.Alignment);
                endPos += param.Size;
            }
            maxEnd = Math.Max(maxEnd, endPos);
        }

        var totalSize = TypeClassifier.Align(maxEnd, structAlignment);
        return (totalSize, structAlignment);
    }

    /// <summary>
    /// Estimates the size of a union the runtime lays out — every field under
    /// <see cref="LayoutStrategy.Auto"/>; the storage struct plus the fields beside it under
    /// <see cref="LayoutStrategy.Hybrid"/>.
    /// </summary>
    /// <remarks>
    /// Fields go in declaration order, as the generated code declares them. The runtime keeps that
    /// order only for a struct without references; one with references it reorders, placing the
    /// largest fields first, so for those this sorts by alignment first and the result is an estimate.
    /// </remarks>
    static (int Size, int Alignment) EstimateSequential(
        LayoutStrategy strategy,
        IReadOnlyList<VariantModel> variants,
        IReadOnlyList<FieldModel> commonFields,
        int storageSize,
        int storageAlignment)
    {
        var fields = new List<(int Size, int Alignment)>();
        var holdsReferences = false;

        if (strategy == LayoutStrategy.Hybrid)
        {
            fields.Add((storageSize, storageAlignment));
            holdsReferences = HoldsReferences(variants, commonFields);
        }
        else
        {
            fields.Add((1, 1)); // tag
        }

        foreach (var field in commonFields)
        {
            fields.Add((field.Size, field.Alignment));
            holdsReferences |= !field.IsUnmanaged;
        }

        foreach (var variant in variants)
        {
            foreach (var param in variant.Parameters)
            {
                if (strategy == LayoutStrategy.Hybrid && CanOverlap(param))
                {
                    continue;
                }

                fields.Add((param.Size, param.Alignment));
                holdsReferences |= !param.IsUnmanaged;
            }
        }

        foreach (var (size, alignment) in fields)
        {
            if (size < 0 || alignment < 0)
            {
                return (-1, -1);
            }
        }

        var ordered = holdsReferences
            ? fields.OrderByDescending(f => f.Alignment)
            : (IEnumerable<(int Size, int Alignment)>)fields;

        var offset = 0;
        var maxAlignment = 1;
        foreach (var (size, alignment) in ordered)
        {
            offset = TypeClassifier.Align(offset, alignment) + size;
            maxAlignment = Math.Max(maxAlignment, alignment);
        }

        return (TypeClassifier.Align(offset, maxAlignment), maxAlignment);
    }

    static bool HoldsReferences(IReadOnlyList<VariantModel> variants, IReadOnlyList<FieldModel> commonFields)
    {
        foreach (var field in commonFields)
        {
            if (!field.IsUnmanaged)
            {
                return true;
            }
        }

        foreach (var variant in variants)
        {
            foreach (var param in variant.Parameters)
            {
                if (!param.IsUnmanaged)
                {
                    return true;
                }
            }
        }

        return false;
    }

    static int CountRefFields(VariantModel variant)
    {
        var count = 0;
        foreach (var param in variant.Parameters)
        {
            if (CanOverlap(param) && IsRefField(param))
            {
                count++;
            }
        }

        return count;
    }

    static bool IsValueZoneField(FieldModel field) => CanOverlap(field) && !IsRefField(field);

    /// <summary>
    /// A field is a reference type if it's not unmanaged (not a value type or contains refs).
    /// Only meaningful for a field that <see cref="CanOverlap"/>, where the one managed kind left is a reference.
    /// </summary>
    public static bool IsRefField(FieldModel field) => !field.IsUnmanaged;
}
