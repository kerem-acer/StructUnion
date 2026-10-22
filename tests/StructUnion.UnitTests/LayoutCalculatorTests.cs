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

        var result = LayoutCalculator.DetermineStrategy(variants, [], isGeneric: false);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Explicit);
    }

    [Test]
    public async Task DetermineStrategy_RefTypes_ReturnsExplicit()
    {
        var variants = MakeVariants(
            ("Text", MakeFields(("value", "string", false, 8, 8))),
            ("Num", MakeFields(("value", "int", true, 4, 4))));

        var result = LayoutCalculator.DetermineStrategy(variants, [], isGeneric: false);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Explicit);
    }

    [Test]
    public async Task DetermineStrategy_UnknownSize_ReturnsAuto()
    {
        var variants = MakeVariants(
            ("Some", MakeFields(("value", "T", true, -1, -1))));

        var result = LayoutCalculator.DetermineStrategy(variants, [], isGeneric: true);
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
        var common = new FieldModel[] { new("Id", "System.Guid", "public", true, true, 16, 4) };
        var variants = MakeVariants(
            ("Text", MakeFields(("s", "string", false, 8, 8))));


        var (refOffset, _) = LayoutCalculator.ComputeZoneOffsets(common, variants);

        // tag(1) + Align(1,4)=4 + Guid(16) = 20, ref zone at Align(20,8) = 24
        await Assert.That(refOffset).IsEqualTo(24);
    }

    [Test]
    public async Task ComputeCommonFieldOffset_SingleInt()
    {
        var common = new FieldModel[] { new("x", "int", "public", true, true, 4, 4) };
        var offset = LayoutCalculator.ComputeCommonFieldOffset(common, 0);

        await Assert.That(offset).IsEqualTo(4); // Align(1, 4)
    }

    [Test]
    public async Task ComputeCommonFieldOffset_TwoFields()
    {
        var common = new FieldModel[]
        {
            new("a", "int", "public", true, true, 4, 4),
            new("b", "byte", "public", true, true, 1, 1)
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
        var field = new FieldModel("x", "int", "public", true, true, 4, 4);
        await Assert.That(LayoutCalculator.IsRefField(field)).IsFalse();
    }

    [Test]
    public async Task IsRefField_RefType_ReturnsTrue()
    {
        var field = new FieldModel("s", "string", "public", false, false, 8, 8);
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
            new("a", "byte", "public", true, true, 1, 1),
            new("b", "int", "public", true, true, 4, 4),
            new("c", "byte", "public", true, true, 1, 1)
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
    public async Task DetermineStrategy_ManagedValueTypeOnly_ReturnsAuto()
    {
        // A managed value type (value type containing references, e.g. ValueTuple<string, int>)
        // cannot participate in explicit overlapping layout, and with nothing else to overlap
        // there is no point in nested storage.
        var managedStructField = new FieldModel("t", "ValueTuple<string, int>", "public",
            IsValueType: true, IsUnmanaged: false, Size: 16, Alignment: 8);
        var variants = ImmutableArray.Create(
            new VariantModel("A", ImmutableArray.Create(managedStructField).ToEquatableArray(), 1));

        var result = LayoutCalculator.DetermineStrategy(variants, [], isGeneric: false);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Auto);
    }

    [Test]
    public async Task DetermineStrategy_UnmanagedRefStruct_ReturnsAuto()
    {
        // A ref struct whose own fields are all unmanaged reports IsUnmanagedType == true and has a
        // perfectly computable size, so the managed-value-type test alone lets it reach Explicit
        // layout. It still carries a byref, which cannot sit at a FieldOffset — and the C# compiler
        // accepts LayoutKind.Explicit on a ref struct silently, so nothing downstream would catch it.
        var refStructField = new FieldModel("p", "Plain", "public",
            IsValueType: true, IsUnmanaged: true, Size: 4, Alignment: 4, IsRefLike: true);
        var variants = ImmutableArray.Create(
            new VariantModel("A", ImmutableArray.Create(refStructField).ToEquatableArray(), 1));

        var result = LayoutCalculator.DetermineStrategy(variants, [], isGeneric: false);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Auto);
    }

    [Test]
    public async Task DetermineStrategy_RefLikeCommonField_ReturnsAuto()
    {
        var common = new FieldModel[]
        {
            new("data", "System.Span<byte>", "public",
                IsValueType: true, IsUnmanaged: false, Size: 16, Alignment: 8, IsRefLike: true)
        };
        var variants = MakeVariants(
            ("A", MakeFields(("x", "int", true, 4, 4))));

        var result = LayoutCalculator.DetermineStrategy(variants, common, isGeneric: false);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Auto);
    }

    [Test]
    public async Task DetermineStrategy_UnknownCommonFieldSize_ReturnsAuto()
    {
        var common = new FieldModel[] { new("id", "T", "public", true, true, -1, -1) };
        var variants = MakeVariants(
            ("A", MakeFields(("x", "int", true, 4, 4))));

        var result = LayoutCalculator.DetermineStrategy(variants, common, isGeneric: true);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Auto);
    }

    [Test]
    public async Task DetermineStrategy_GenericWithKnownSizes_ReturnsAuto()
    {
        // The CLR refuses explicit layout on a generic type even when no field mentions a type
        // parameter: "generic types cannot have explicit layout".
        var variants = MakeVariants(
            ("Items", MakeFields(("items", "List<T>", false, 8, 8))),
            ("Text", MakeFields(("s", "string", false, 8, 8))));

        var result = LayoutCalculator.DetermineStrategy(variants, [], isGeneric: true);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Auto);
    }

    [Test]
    public async Task DetermineStrategy_ManagedValueTypeBesideOverlappingCases_ReturnsHybrid()
    {
        var variants = ImmutableArray.Create(
            new VariantModel("A", MakeFields(("a", "string", false, 8, 8)).ToEquatableArray(), 1),
            new VariantModel("B", MakeFields(("b", "string", false, 8, 8)).ToEquatableArray(), 2),
            new VariantModel("E", ImmutableArray.Create(ManagedStruct("names", 8)).ToEquatableArray(), 3));

        var result = LayoutCalculator.DetermineStrategy(variants, [], isGeneric: false);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Hybrid);
    }

    [Test]
    public async Task DetermineStrategy_UnknownSizeInNonGenericUnion_ReturnsHybrid()
    {
        // An error type — say, the struct a record template is about to generate — has no size the
        // generator can see, so it goes beside the storage struct rather than into it.
        var variants = MakeVariants(
            ("A", MakeFields(("a", "string", false, 8, 8))),
            ("P", MakeFields(("p", "Pair", false, -1, -1))));

        var result = LayoutCalculator.DetermineStrategy(variants, [], isGeneric: false);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Hybrid);
    }

    [Test]
    public async Task DetermineStrategy_ManagedCommonField_ReturnsHybrid()
    {
        var common = new FieldModel[] { ManagedStruct("pair", 16) };
        var variants = MakeVariants(
            ("A", MakeFields(("x", "int", true, 4, 4))),
            ("B", MakeFields(("s", "string", false, 8, 8))));

        var result = LayoutCalculator.DetermineStrategy(variants, common, isGeneric: false);
        await Assert.That(result).IsEqualTo(LayoutStrategy.Hybrid);
    }

    [Test]
    public async Task Plan_Hybrid_OverlapsOnlyStorageFields()
    {
        // Four string cases share one slot in storage; the managed struct sits beside it.
        var variants = ImmutableArray.Create(
            new VariantModel("A", MakeFields(("a", "string", false, 8, 8)).ToEquatableArray(), 1),
            new VariantModel("B", MakeFields(("b", "string", false, 8, 8)).ToEquatableArray(), 2),
            new VariantModel("C", MakeFields(("c", "string", false, 8, 8)).ToEquatableArray(), 3),
            new VariantModel("D", MakeFields(("d", "string", false, 8, 8)).ToEquatableArray(), 4),
            new VariantModel("E", ImmutableArray.Create(ManagedStruct("names", 8)).ToEquatableArray(), 5));

        var plan = LayoutCalculator.Plan(variants, [], isGeneric: false);

        await Assert.That(plan.Strategy).IsEqualTo(LayoutStrategy.Hybrid);
        await Assert.That(plan.RefZoneOffset).IsEqualTo(8);
        await Assert.That(plan.TotalSize).IsEqualTo(16);       // storage: tag + one shared slot
        await Assert.That(plan.EstimatedSize).IsEqualTo(24);   // storage + Names
        await Assert.That(plan.HoldsReferences).IsTrue();
    }

    [Test]
    public async Task Plan_Hybrid_CommonFieldsStayOutOfStorage()
    {
        // In storage the decimal would push the ref zone to 24; beside it, storage is tag + string.
        var common = new FieldModel[] { new("amount", "decimal", "public", true, true, 16, 8) };
        var variants = ImmutableArray.Create(
            new VariantModel("A", MakeFields(("a", "string", false, 8, 8)).ToEquatableArray(), 1),
            new VariantModel("E", ImmutableArray.Create(ManagedStruct("names", 8)).ToEquatableArray(), 2));

        var plan = LayoutCalculator.Plan(variants, common, isGeneric: false);

        await Assert.That(plan.TotalSize).IsEqualTo(16);
        await Assert.That(plan.EstimatedSize).IsEqualTo(40); // storage 16 + decimal 16 + Names 8
    }

    [Test]
    public async Task Plan_Auto_WithoutReferences_FollowsDeclarationOrder()
    {
        // A struct without references keeps its declaration order:
        // tag @0, a @1, b @4, c @8 → 9, rounded to 12.
        var variants = MakeVariants(
            ("A", MakeFields(("a", "byte", true, 1, 1))),
            ("B", MakeFields(("b", "int", true, 4, 4))),
            ("C", MakeFields(("c", "byte", true, 1, 1))));

        var plan = LayoutCalculator.Plan(variants, [], isGeneric: true);

        await Assert.That(plan.Strategy).IsEqualTo(LayoutStrategy.Auto);
        await Assert.That(plan.EstimatedSize).IsEqualTo(12);
        await Assert.That(plan.EstimatedAlignment).IsEqualTo(4);
        await Assert.That(plan.HoldsReferences).IsFalse();
    }

    [Test]
    public async Task Plan_Auto_CountsEveryCase()
    {
        // Sequential fields sit side by side: 9 strings + the tag, not just the largest case.
        var variants = MakeVariants(
            [.. Enumerable.Range(0, 9).Select(i => ($"V{i}", MakeFields(("s", "string", false, 8, 8))))]);

        var plan = LayoutCalculator.Plan(variants, [], isGeneric: true);

        await Assert.That(plan.EstimatedSize).IsEqualTo(80);
    }

    [Test]
    public async Task Plan_UnknownSize_EstimatesNothing()
    {
        var variants = MakeVariants(
            ("Some", MakeFields(("value", "T", true, -1, -1))));

        var plan = LayoutCalculator.Plan(variants, [], isGeneric: true);

        await Assert.That(plan.EstimatedSize).IsEqualTo(-1);
    }

    [Test]
    public async Task ComputeVariantFieldOffset_SkipsFieldsOutsideStorage()
    {
        // The managed struct before the int is not in storage, so it takes no room in the value zone.
        var variant = new VariantModel("A", ImmutableArray.Create(
            ManagedStruct("names", 8),
            new FieldModel("n", "int", "public", true, true, 4, 4)).ToEquatableArray(), 1);

        await Assert.That(LayoutCalculator.ComputeVariantFieldOffset(variant, 1, refZoneOffset: 8, valueZoneOffset: 4))
            .IsEqualTo(4);
    }

    [Test]
    [Arguments("string", false, false, 8, false, true)]
    [Arguments("int", true, true, 4, false, true)]
    [Arguments("ValueTuple<string, int>", true, false, 16, false, false)]
    [Arguments("Plain", true, true, 4, true, false)]
    [Arguments("Pair", false, false, -1, false, false)]
    public async Task CanOverlap(string type, bool isValueType, bool isUnmanaged, int size, bool isRefLike, bool expected)
    {
        var field = new FieldModel("x", type, "public", isValueType, isUnmanaged, size, size, IsRefLike: isRefLike);
        await Assert.That(LayoutCalculator.CanOverlap(field)).IsEqualTo(expected);
    }

    // -- Helpers --

    static ImmutableArray<VariantModel> MakeVariants(
        params (string Name, ImmutableArray<FieldModel> Params)[] variants) =>
        [.. variants.Select((v, i) => new VariantModel(v.Name, v.Params.ToEquatableArray(), (byte)(i + 1)))];

    static FieldModel ManagedStruct(string name, int size) =>
        new(name, "Names", "public", IsValueType: true, IsUnmanaged: false, Size: size, Alignment: 8);

    static ImmutableArray<FieldModel> MakeFields(
        params (string Name, string Type, bool IsUnmanaged, int Size, int Align)[] fields) =>
        [.. fields.Select(f => new FieldModel(f.Name, f.Type, "public", f.IsUnmanaged, f.IsUnmanaged, f.Size, f.Align))];
}
