using System.Collections.Immutable;
using StructUnion.Generator.Infrastructure;
using StructUnion.Generator.Models;

namespace StructUnion.UnitTests;

public class UnionModelTests
{
    static UnionModel MakeModel(
        string ns = "",
        string name = "Shape",
        string[]? containingTypes = null,
        TypeParameterModel[]? typeParams = null,
        FieldModel[]? commonFields = null,
        bool nativeUnion = false) =>
        new(
            ns,
            (containingTypes ?? []).ToImmutableArray().ToEquatableArray(),
            "public", name,
            (typeParams ?? []).ToImmutableArray().ToEquatableArray(),
            ImmutableArray.Create(
                new VariantModel("Circle",
                    ImmutableArray.Create(new FieldModel("radius", "double", "public", true, true, 8, 8)).ToEquatableArray(),
                    1)).ToEquatableArray(),
            (commonFields ?? []).ToImmutableArray().ToEquatableArray(),
            LayoutStrategy.Explicit, true, 8, 8, 16, 8, GenerationMode.PartialStruct, "Tag", false, false, nativeUnion, nativeUnion);

    [Test]
    public async Task HasCommonFields_Empty_ReturnsFalse()
    {
        var model = MakeModel();
        await Assert.That(model.HasCommonFields).IsFalse();
    }

    [Test]
    public async Task HasCommonFields_NonEmpty_ReturnsTrue()
    {
        var model = MakeModel(commonFields: [new("id", "int", "public", true, true, 4, 4)]);
        await Assert.That(model.HasCommonFields).IsTrue();
    }

    [Test]
    public async Task TagField_ReturnsTag()
    {
        var model = MakeModel();
        await Assert.That(model.TagField).IsEqualTo("_tag");
    }

    [Test]
    public async Task VariantModel_FieldName_FormatsCorrectly()
    {
        var variant = new VariantModel("Circle",
            ImmutableArray.Create(new FieldModel("radius", "double", "public", true, true, 8, 8)).ToEquatableArray(), 1);
        await Assert.That(variant.FieldName("radius")).IsEqualTo("_circle_radius");
    }

    [Test]
    public async Task VariantModel_FieldName_MixedCase()
    {
        var variant = new VariantModel("UserCreated",
            ImmutableArray<FieldModel>.Empty.ToEquatableArray(), 1);
        await Assert.That(variant.FieldName("Name")).IsEqualTo("_usercreated_Name");
    }

    [Test]
    public async Task VariantModel_NameLower()
    {
        var variant = new VariantModel("Circle",
            ImmutableArray<FieldModel>.Empty.ToEquatableArray(), 1);
        await Assert.That(variant.NameLower).IsEqualTo("circle");
    }

    [Test]
    public async Task FullHintName_GlobalNamespace()
    {
        var model = MakeModel(ns: "");
        await Assert.That(model.FullHintName).IsEqualTo("Shape");
    }

    [Test]
    public async Task FullHintName_WithNamespace()
    {
        var model = MakeModel(ns: "MyApp.Models");
        await Assert.That(model.FullHintName).IsEqualTo("MyApp.Models.Shape");
    }

    [Test]
    public async Task FullHintName_WithContainingTypes()
    {
        var model = MakeModel(ns: "MyApp", containingTypes: ["partial class Outer"]);
        await Assert.That(model.FullHintName).IsEqualTo("MyApp.Outer.Shape");
    }

    [Test]
    public async Task FullHintName_WithGenericContainingType()
    {
        var model = MakeModel(ns: "MyApp", containingTypes: ["partial class Outer<T>"]);
        await Assert.That(model.FullHintName).IsEqualTo("MyApp.Outer.Shape");
    }

    [Test]
    public async Task TypeNameWithParameters_NoParams()
    {
        var model = MakeModel();
        await Assert.That(model.TypeNameWithParameters).IsEqualTo("Shape");
    }

    [Test]
    public async Task TypeNameWithParameters_OneParam()
    {
        var tp = new TypeParameterModel("T", ImmutableArray<string>.Empty.ToEquatableArray());
        var model = MakeModel(name: "Option", typeParams: [tp]);
        await Assert.That(model.TypeNameWithParameters).IsEqualTo("Option<T>");
    }

    [Test]
    public async Task TypeNameWithParameters_TwoParams()
    {
        var tp1 = new TypeParameterModel("TOk", ImmutableArray<string>.Empty.ToEquatableArray());
        var tp2 = new TypeParameterModel("TError", ImmutableArray<string>.Empty.ToEquatableArray());
        var model = MakeModel(name: "Result", typeParams: [tp1, tp2]);
        await Assert.That(model.TypeNameWithParameters).IsEqualTo("Result<TOk, TError>");
    }

    [Test]
    public async Task FullyQualifiedName_GlobalNamespace()
    {
        var model = MakeModel();
        await Assert.That(model.FullyQualifiedName).IsEqualTo("global::Shape");
    }

    [Test]
    public async Task FullyQualifiedName_WithNamespace()
    {
        var model = MakeModel(ns: "MyApp.Models");
        await Assert.That(model.FullyQualifiedName).IsEqualTo("global::MyApp.Models.Shape");
    }

    [Test]
    public async Task FullyQualifiedName_WithContainingTypes()
    {
        var model = MakeModel(ns: "MyApp", containingTypes: ["partial class Outer"]);
        await Assert.That(model.FullyQualifiedName).IsEqualTo("global::MyApp.Outer.Shape");
    }

    [Test]
    public async Task FullyQualifiedName_KeepsGenericArgumentsOfContainingType()
    {
        // Unlike the hint name, generic arguments must survive — they are part of the type name.
        var model = MakeModel(ns: "MyApp", containingTypes: ["partial class Outer<T>"]);
        await Assert.That(model.FullyQualifiedName).IsEqualTo("global::MyApp.Outer<T>.Shape");
    }

    [Test]
    public async Task FullyQualifiedName_IncludesOwnTypeParameters()
    {
        var tp = new TypeParameterModel("T", ImmutableArray<string>.Empty.ToEquatableArray());
        var model = MakeModel(ns: "MyApp", name: "Option", typeParams: [tp]);
        await Assert.That(model.FullyQualifiedName).IsEqualTo("global::MyApp.Option<T>");
    }

    [Test]
    public async Task NativeUnion_ParticipatesInEquality()
    {
        // UnionModel is the incremental generator's cache key; a new field that did not take part
        // in structural equality would leave stale output cached when the option changed.
        await Assert.That(MakeModel(nativeUnion: true)).IsNotEqualTo(MakeModel(nativeUnion: false));
    }

    [Test]
    public async Task EmitCases_TrueWhenNativeUnion()
    {
        await Assert.That(MakeModel(nativeUnion: true).EmitCases).IsTrue();
        await Assert.That(MakeModel(nativeUnion: false).EmitCases).IsFalse();
    }
}
