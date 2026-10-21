namespace StructUnion.IntegrationTests.ConditionalAttributes;

/// <summary>
/// Reading a type's attributes loads every attribute's assembly. A <c>[StructUnion]</c> left in
/// metadata would make that throw FileNotFoundException in any app that ships without
/// <c>StructUnion.dll</c>, which is what <c>PrivateAssets="all"</c> does.
/// </summary>
public class ConditionalAttributeTests
{
    [Test]
    [Arguments(typeof(CustomTagName.Event))]
    [Arguments(typeof(ComplexTypes.JsonValueRecord))]
    [Arguments(typeof(ComplexTypes.JsonValue))]
    public async Task UnionTypes_CarryNoStructUnionAttribute(Type type)
    {
        var fromStructUnion = type.GetCustomAttributes(inherit: false)
            .Where(a => a.GetType().Assembly == typeof(StructUnionAttribute).Assembly);

        await Assert.That(fromStructUnion).IsEmpty();
    }
}
