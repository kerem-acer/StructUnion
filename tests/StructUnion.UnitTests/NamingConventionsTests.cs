using StructUnion.Generator.Parsing;

namespace StructUnion.UnitTests;

public class NamingConventionsTests
{
    [Test]
    public async Task DeriveStructName_TrimsRecordSuffix()
    {
        var result = NamingConventions.DeriveStructName("ShapeRecord", null, "Record");
        await Assert.That(result).IsEqualTo("Shape");
    }

    [Test]
    public async Task DeriveStructName_ExplicitNameTakesPriority()
    {
        var result = NamingConventions.DeriveStructName("ShapeRecord", "MyShape", "Record");
        await Assert.That(result).IsEqualTo("MyShape");
    }

    [Test]
    public async Task DeriveStructName_NoSuffixMatch_ReturnsAsIs()
    {
        var result = NamingConventions.DeriveStructName("ShapeDef", null, "Record");
        await Assert.That(result).IsEqualTo("ShapeDef");
    }

    [Test]
    public async Task DeriveStructName_CustomSuffix()
    {
        var result = NamingConventions.DeriveStructName("ShapeDef", null, "Def");
        await Assert.That(result).IsEqualTo("Shape");
    }

    [Test]
    public async Task DeriveStructName_NameEqualsJustSuffix_ReturnsAsIs()
    {
        var result = NamingConventions.DeriveStructName("Record", null, "Record");
        await Assert.That(result).IsEqualTo("Record");
    }

    [Test]
    public async Task DeriveStructName_EmptySuffix_ReturnsAsIs()
    {
        var result = NamingConventions.DeriveStructName("ShapeRecord", null, "");
        await Assert.That(result).IsEqualTo("ShapeRecord");
    }
}
