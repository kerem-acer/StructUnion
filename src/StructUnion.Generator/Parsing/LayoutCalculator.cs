using StructUnion.Generator.Models;

namespace StructUnion.Generator.Parsing;

/// <summary>
/// Computes layout strategy and field offsets for a union.
///
/// Two-zone explicit layout for mixed ref/value unions:
///   [tag] [common fields] [ref zone (overlapping)] [value zone (overlapping)]
///
/// CLR allows ref-ref overlap and value-value overlap at the same FieldOffset,
/// but NOT ref-value. This two-zone approach gives us Explicit layout for all
/// unions except those with unconstrained generic type parameters.
/// </summary>
static class LayoutCalculator
{
    public static LayoutStrategy DetermineStrategy(
        IReadOnlyList<VariantModel> variants,
        IReadOnlyList<FieldModel> commonFields)
    {
        // Only fall back to Auto when sizes are unknowable (generic T)
        foreach (var field in commonFields)
        {
            if (field.Size < 0 || field.Alignment < 0)
            {
                return LayoutStrategy.Auto;
            }
        }

        foreach (var variant in variants)
        {
            foreach (var param in variant.Parameters)
            {
                if (param.Size < 0 || param.Alignment < 0)
                {
                    return LayoutStrategy.Auto;
                }
            }
        }

        return LayoutStrategy.Explicit;
    }

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
                var refCount = 0;
                foreach (var param in variant.Parameters)
                {
                    if (IsRefField(param))
                    {
                        refCount++;
                    }
                }

                maxRefSlots = Math.Max(maxRefSlots, refCount);
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
                if (IsRefField(param))
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
    /// Ref fields go to the ref zone, value fields go to the value zone.
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
                if (IsRefField(variant.Parameters[i]))
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
            if (IsRefField(variant.Parameters[i]))
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
            var refCount = 0;
            foreach (var param in variant.Parameters)
            {
                if (!param.IsUnmanaged)
                {
                    refCount++;
                    hasRefFields = true;
                }
                structAlignment = Math.Max(structAlignment, param.Alignment);
            }
            maxRefSlots = Math.Max(maxRefSlots, refCount);
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
            for (var i = 0; i < variant.Parameters.Count; i++)
            {
                var param = variant.Parameters[i];
                if (!param.IsUnmanaged)
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
    /// A field is a reference type if it's not unmanaged (not a value type or contains refs).
    /// </summary>
    public static bool IsRefField(FieldModel field) => !field.IsUnmanaged;
}
