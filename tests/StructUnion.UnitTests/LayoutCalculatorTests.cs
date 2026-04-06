using System.Collections.Immutable;
using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;
using StructUnion.Generator.Parsing;

namespace StructUnion.UnitTests;

public class LayoutCalculatorTests
{
    [Test]
    public async Task DetermineStrategy_AllUnmanaged_ReturnsExplicit()
    {
        var variants = MakeVariants(
            ("Circle", MakeFields(("radius", "double", true, 8, 8))),
            ("Rect", MakeFields(("w", "double", true, 8, 8), ("h", "double", true, 8, 8))));

        var result = LayoutCalculator.DetermineStrategy(variants, []);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Explicit);
    }

    [Test]
    public async Task DetermineStrategy_RefTypes_ReturnsExplicit()
    {
        var variants = MakeVariants(
            ("Text", MakeFields(("value", "string", false, 8, 8))),
            ("Num", MakeFields(("value", "int", true, 4, 4))));

        var result = LayoutCalculator.DetermineStrategy(variants, []);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Explicit);
    }

    [Test]
    public async Task DetermineStrategy_UnknownSize_ReturnsAuto()
    {
        var variants = MakeVariants(
            ("Some", MakeFields(("value", "T", true, -1, -1))));

        var result = LayoutCalculator.DetermineStrategy(variants, []);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Auto);
    }

    [Test]
    public async Task ComputeZoneOffsets_NoCommonFields_ValueOnly()
    {
        var variants = MakeVariants(
            ("A", MakeFields(("x", "double", true, 8, 8))),
            ("B", MakeFields(("y", "int", true, 4, 4))));

        var (refOffset, valueOffset) = LayoutCalculator.ComputeZoneOffsets([], variants);

        // No ref fields, so refOffset == tag end, valueOffset aligned for double
        await Assert.That(refOffset).IsEqualTo(1);
        await Assert.That(valueOffset).IsEqualTo(8); // Align(1, 8) for double
    }

    [Test]
    public async Task ComputeZoneOffsets_MixedRefValue_ValueFitsInGap()
    {
        var variants = MakeVariants(
            ("Text", MakeFields(("s", "string", false, 8, 8))),
            ("Num", MakeFields(("n", "int", true, 4, 4))));

        var (refOffset, valueOffset) = LayoutCalculator.ComputeZoneOffsets([], variants);

        await Assert.That(refOffset).IsEqualTo(8); // Align(1, 8) for ref
        await Assert.That(valueOffset).IsEqualTo(4); // int packed in gap: Align(1, 4) = 4
    }

    [Test]
    public async Task ComputeZoneOffsets_MixedRefValue_ValueDoesNotFitInGap()
    {
        // double (8B align 8) can't fit in gap — collides with ref zone at @8
        var variants = MakeVariants(
            ("Text", MakeFields(("s", "string", false, 8, 8))),
            ("Num", MakeFields(("n", "double", true, 8, 8))));

        var (refOffset, valueOffset) = LayoutCalculator.ComputeZoneOffsets([], variants);

        await Assert.That(refOffset).IsEqualTo(8);
        await Assert.That(valueOffset).IsEqualTo(16); // after ref zone (can't fit in gap)
    }

    [Test]
    public async Task ComputeZoneOffsets_WithCommonGuid()
    {
        var common = new FieldModel[] { new("Id", "System.Guid", "public", true, 16, 4) };
        var variants = MakeVariants(
            ("Text", MakeFields(("s", "string", false, 8, 8))));


        var (refOffset, _) = LayoutCalculator.ComputeZoneOffsets(common, variants);

        // tag(1) + Align(1,4)=4 + Guid(16) = 20, ref zone at Align(20,8) = 24
        await Assert.That(refOffset).IsEqualTo(24);
    }

    [Test]
    public async Task ComputeCommonFieldOffset_SingleInt()
    {
        var common = new FieldModel[] { new("x", "int", "public", true, 4, 4) };
        var offset = LayoutCalculator.ComputeCommonFieldOffset(common, 0);

        await Assert.That(offset).IsEqualTo(4); // Align(1, 4)
    }

    [Test]
    public async Task ComputeCommonFieldOffset_TwoFields()
    {
        var common = new FieldModel[]
        {
            new("a", "int", "public", true, 4, 4),
            new("b", "byte", "public", true, 1, 1)
        };

        var offset0 = LayoutCalculator.ComputeCommonFieldOffset(common, 0);
        var offset1 = LayoutCalculator.ComputeCommonFieldOffset(common, 1);

        await Assert.That(offset0).IsEqualTo(4); // Align(1, 4)
        await Assert.That(offset1).IsEqualTo(8); // 4 + 4 = 8, Align(8, 1) = 8
    }

    [Test]
    public async Task ComputeVariantFieldOffset_ValueField()
    {
        var variant = new VariantModel("A",
            MakeFields(("x", "double", true, 8, 8)).ToEquatableArray(), 1);

        var offset = LayoutCalculator.ComputeVariantFieldOffset(variant, 0, refZoneOffset: 8, valueZoneOffset: 8);
        await Assert.That(offset).IsEqualTo(8);
    }

    [Test]
    public async Task ComputeVariantFieldOffset_RefField()
    {
        var variant = new VariantModel("A",
            MakeFields(("s", "string", false, 8, 8)).ToEquatableArray(), 1);

        var offset = LayoutCalculator.ComputeVariantFieldOffset(variant, 0, refZoneOffset: 8, valueZoneOffset: 16);
        await Assert.That(offset).IsEqualTo(8); // ref zone
    }

    [Test]
    public async Task ComputeVariantFieldOffset_MixedRefAndValue()
    {
        var variant = new VariantModel("Both",
            MakeFields(
                ("name", "string", false, 8, 8),
                ("age", "int", true, 4, 4)).ToEquatableArray(), 1);

        var refOff = LayoutCalculator.ComputeVariantFieldOffset(variant, 0, refZoneOffset: 8, valueZoneOffset: 16);
        var valOff = LayoutCalculator.ComputeVariantFieldOffset(variant, 1, refZoneOffset: 8, valueZoneOffset: 16);

        await Assert.That(refOff).IsEqualTo(8); // ref zone
        await Assert.That(valOff).IsEqualTo(16); // value zone
    }

    // ── IsRefField ──

    [Test]
    public async Task IsRefField_Unmanaged_ReturnsFalse()
    {
        var field = new FieldModel("x", "int", "public", true, 4, 4);
        await Assert.That(LayoutCalculator.IsRefField(field)).IsFalse();
    }

    [Test]
    public async Task IsRefField_RefType_ReturnsTrue()
    {
        var field = new FieldModel("s", "string", "public", false, 8, 8);
        await Assert.That(LayoutCalculator.IsRefField(field)).IsTrue();
    }

    // ── ComputeZoneOffsets: more scenarios ──

    [Test]
    public async Task ComputeZoneOffsets_MultipleRefsPerVariant()
    {
        var variants = MakeVariants(
            ("A", MakeFields(("s1", "string", false, 8, 8), ("s2", "string", false, 8, 8))),
            ("B", MakeFields(("s", "string", false, 8, 8))));

        var (refOffset, valueOffset) = LayoutCalculator.ComputeZoneOffsets([], variants);

        await Assert.That(refOffset).IsEqualTo(8);
        // Max ref slots = 2 (variant A), so value zone at 8 + 2*8 = 24
        await Assert.That(valueOffset).IsEqualTo(24);
    }

    [Test]
    public async Task ComputeZoneOffsets_OnlyRefFields()
    {
        var variants = MakeVariants(
            ("A", MakeFields(("s", "string", false, 8, 8))),
            ("B", MakeFields(("arr", "int[]", false, 8, 8))));

        var (refOffset, valueOffset) = LayoutCalculator.ComputeZoneOffsets([], variants);

        await Assert.That(refOffset).IsEqualTo(8);
        await Assert.That(valueOffset).IsEqualTo(16); // after 1 ref slot
    }

    [Test]
    public async Task ComputeZoneOffsets_OnlyValueFields()
    {
        var variants = MakeVariants(
            ("A", MakeFields(("x", "int", true, 4, 4))),
            ("B", MakeFields(("y", "double", true, 8, 8))));

        var (refOffset, valueOffset) = LayoutCalculator.ComputeZoneOffsets([], variants);

        // No ref fields, refOffset = 1 (just after tag)
        await Assert.That(refOffset).IsEqualTo(1);
        await Assert.That(valueOffset).IsEqualTo(8); // Align(1, 8) for double
    }

    // ── ComputeCommonFieldOffset: more scenarios ──

    [Test]
    public async Task ComputeCommonFieldOffset_ThreeFields()
    {
        var common = new FieldModel[]
        {
            new("a", "byte", "public", true, 1, 1),
            new("b", "int", "public", true, 4, 4),
            new("c", "byte", "public", true, 1, 1)
        };

        await Assert.That(LayoutCalculator.ComputeCommonFieldOffset(common, 0)).IsEqualTo(1);   // right after tag
        await Assert.That(LayoutCalculator.ComputeCommonFieldOffset(common, 1)).IsEqualTo(4);   // Align(2, 4)
        await Assert.That(LayoutCalculator.ComputeCommonFieldOffset(common, 2)).IsEqualTo(8);   // 4 + 4 = 8
    }

    // ── ComputeVariantFieldOffset: more scenarios ──

    [Test]
    public async Task ComputeVariantFieldOffset_ThreeMixedFields()
    {
        var variant = new VariantModel("V",
            MakeFields(
                ("s1", "string", false, 8, 8),   // ref
                ("n", "int", true, 4, 4),          // value
                ("s2", "string", false, 8, 8)      // ref
            ).ToEquatableArray(), 1);

        // Ref zone: s1 at refZone+0, s2 at refZone+8
        // Value zone: n at valueZone+0
        var s1Off = LayoutCalculator.ComputeVariantFieldOffset(variant, 0, 8, 24);
        var nOff = LayoutCalculator.ComputeVariantFieldOffset(variant, 1, 8, 24);
        var s2Off = LayoutCalculator.ComputeVariantFieldOffset(variant, 2, 8, 24);

        await Assert.That(s1Off).IsEqualTo(8);   // first ref
        await Assert.That(nOff).IsEqualTo(24);    // first value
        await Assert.That(s2Off).IsEqualTo(16);   // second ref
    }

    [Test]
    public async Task ComputeVariantFieldOffset_TwoSequentialValues()
    {
        var variant = new VariantModel("V",
            MakeFields(
                ("a", "int", true, 4, 4),
                ("b", "double", true, 8, 8)
            ).ToEquatableArray(), 1);

        var aOff = LayoutCalculator.ComputeVariantFieldOffset(variant, 0, 8, 8);
        var bOff = LayoutCalculator.ComputeVariantFieldOffset(variant, 1, 8, 8);

        await Assert.That(aOff).IsEqualTo(8);     // value zone start
        await Assert.That(bOff).IsEqualTo(16);    // Align(8+4=12, 8) = 16
    }

    [Test]
    public async Task DetermineStrategy_UnknownCommonFieldSize_ReturnsAuto()
    {
        var common = new FieldModel[] { new("id", "T", "public", true, -1, -1) };
        var variants = MakeVariants(
            ("A", MakeFields(("x", "int", true, 4, 4))));

        var result = LayoutCalculator.DetermineStrategy(variants, common);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Auto);
    }

    // -- Helpers --

    static ImmutableArray<VariantModel> MakeVariants(
        params (string Name, ImmutableArray<FieldModel> Params)[] variants) =>
        [.. variants.Select((v, i) => new VariantModel(v.Name, v.Params.ToEquatableArray(), (byte)(i + 1)))];

    static ImmutableArray<FieldModel> MakeFields(
        params (string Name, string Type, bool IsUnmanaged, int Size, int Align)[] fields) =>
        [.. fields.Select(f => new FieldModel(f.Name, f.Type, "public", f.IsUnmanaged, f.Size, f.Align))];
}
