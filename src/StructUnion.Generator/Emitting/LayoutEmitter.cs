using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;
using StructUnion.Generator.Parsing;

namespace StructUnion.Generator.Emitting;

static class LayoutEmitter
{
    public static void Emit(SourceBuilder sb, UnionModel model)
    {
        if (model.Layout != LayoutStrategy.Explicit)
        {
            return;
        }

        var totalSize = model.TotalSize;
        var structAlignment = model.StructAlignment;

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Struct union with {model.Variants.Count} variant{(model.Variants.Count == 1 ? "" : "s")}.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");

        // ── Collect all fields with (offset, end, name, type, size, label) ──
        var fields = new List<(int Start, int End, string Name, string Type, int Size, string Label)>
        {
            // Tag
            (0, 1, "_tag", "byte", 1, "")
        };

        // Common fields
        for (var i = 0; i < model.CommonFields.Count; i++)
        {
            var field = model.CommonFields[i];
            var offset = LayoutCalculator.ComputeCommonFieldOffset(
                model.CommonFields.AsImmutableArray(), i);
            fields.Add((offset, offset + field.Size, field.Name, ShortTypeName(field.TypeFullyQualified), field.Size, "(common)"));
        }

        // Variant fields
        foreach (var variant in model.Variants)
        {
            for (var j = 0; j < variant.Parameters.Count; j++)
            {
                var param = variant.Parameters[j];
                var offset = LayoutCalculator.ComputeVariantFieldOffset(
                    variant, j, model.RefZoneOffset, model.ValueZoneOffset);
                fields.Add((offset, offset + param.Size, param.Name, ShortTypeName(param.TypeFullyQualified), param.Size, $"({variant.Name})"));
            }
        }

        // Sort by offset, then by label for stable ordering at same offset
        fields.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : string.Compare(a.Label, b.Label, StringComparison.Ordinal));

        // ── Compute actual padding by finding gaps ──
        var offsetWidth = totalSize.ToString().Length;
        var cursor = 0;
        var totalPadding = 0;
        var layoutLines = new List<string>();

        foreach (var field in fields)
        {
            if (field.Start > cursor)
            {
                var gap = field.Start - cursor;
                totalPadding += gap;
                layoutLines.Add(FormatLine(cursor, field.Start, "---", "padding", gap, "", offsetWidth));
            }

            layoutLines.Add(FormatLine(field.Start, field.End, field.Name, field.Type, field.Size, field.Label, offsetWidth));

            if (field.End > cursor)
            {
                cursor = field.End;
            }
        }

        if (cursor < totalSize)
        {
            var gap = totalSize - cursor;
            totalPadding += gap;
            layoutLines.Add(FormatLine(cursor, totalSize, "---", "padding", gap, "", offsetWidth));
        }

        // ── Emit header and layout ──
        var paddingPart = totalPadding > 0 ? $" \u00b7 {totalPadding}B padding" : "";
        sb.AppendLine($"/// <para><b>{totalSize} bytes</b> \u00b7 align {structAlignment}{paddingPart}</para>");
        sb.AppendLine("/// <code>");

        foreach (var line in layoutLines)
        {
            sb.AppendLine($"/// {line}");
        }

        sb.AppendLine("/// </code>");

        // ── Variant size table ──
        sb.AppendLine("/// <para><b>Variants</b></para>");
        sb.AppendLine("/// <code>");

        var maxNameLen = 0;
        foreach (var variant in model.Variants)
        {
            if (variant.Name.Length > maxNameLen)
            {
                maxNameLen = variant.Name.Length;
            }
        }

        foreach (var variant in model.Variants)
        {
            var payload = ComputeVariantPayload(variant);
            sb.AppendLine($"/// {variant.Name.PadRight(maxNameLen)}  {payload,3}B");
        }

        sb.AppendLine("/// </code>");
        sb.AppendLine("/// </remarks>");
    }

    static string FormatLine(
        int start, int end, string name, string typeName,
        int size, string label, int offsetWidth)
    {
        var startStr = start.ToString().PadLeft(offsetWidth);
        var endStr = end.ToString().PadLeft(offsetWidth);
        var sizeStr = $"{size}B";

        var line = $"@{startStr}..{endStr}  {name,-12} {typeName,-10} {sizeStr,4}";

        if (label.Length > 0)
        {
            line += $"  {label}";
        }

        return line;
    }

    static string ShortTypeName(string fullyQualified)
    {
        return fullyQualified switch
        {
            "global::System.Boolean" => "bool",
            "global::System.Byte" => "byte",
            "global::System.SByte" => "sbyte",
            "global::System.Int16" => "short",
            "global::System.UInt16" => "ushort",
            "global::System.Int32" => "int",
            "global::System.UInt32" => "uint",
            "global::System.Int64" => "long",
            "global::System.UInt64" => "ulong",
            "global::System.Single" => "float",
            "global::System.Double" => "double",
            "global::System.Decimal" => "decimal",
            "global::System.Char" => "char",
            "global::System.String" => "string",
            "global::System.Object" => "object",
            "global::System.IntPtr" => "nint",
            "global::System.UIntPtr" => "nuint",
            _ => StripToSimpleName(fullyQualified)
        };
    }

    static string StripToSimpleName(string fullyQualified)
    {
        // "global::Some.Namespace.TypeName" → "TypeName"
        var name = fullyQualified.StartsWith("global::", StringComparison.Ordinal)
            ? fullyQualified.Substring(8)
            : fullyQualified;

        // For generics like "Dictionary<string, object>", find the simple name before '<'
        var genericIdx = name.IndexOf('<');
        if (genericIdx >= 0)
        {
            var baseName = name.Substring(0, genericIdx);
            var lastDot = baseName.LastIndexOf('.');
            if (lastDot >= 0)
            {
                baseName = baseName.Substring(lastDot + 1);
            }
            // XML-escape the generic part
            var genericPart = name.Substring(genericIdx)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
            return baseName + genericPart;
        }

        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name.Substring(dot + 1) : name;
    }

    static int ComputeVariantPayload(VariantModel variant)
    {
        var pos = 0;
        foreach (var param in variant.Parameters)
        {
            pos = TypeClassifier.Align(pos, param.Alignment);
            pos += param.Size;
        }

        return pos;
    }

}
