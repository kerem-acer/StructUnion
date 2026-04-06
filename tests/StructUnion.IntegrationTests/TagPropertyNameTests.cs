namespace StructUnion.IntegrationTests.CustomTagName;

[StructUnion(TagPropertyName = "Kind")]
public readonly partial struct Event
{
    public static partial Event Click(int x, int y);
    public static partial Event KeyPress(char key);
}

public class TagPropertyNameTests
{
    [Test]
    public async Task Kind_ReturnsCorrectEnumValue()
    {
        var e = Event.Click(10, 20);
        await Assert.That(e.Kind).IsEqualTo(Event.Tags.Click);
    }

    [Test]
    public async Task Kind_SwitchExpression_Works()
    {
        var e = Event.KeyPress('a');
        var result = e.Kind switch
        {
            Event.Tags.Click => "click",
            Event.Tags.KeyPress => "key",
            _ => "unknown"
        };

        await Assert.That(result).IsEqualTo("key");
    }
}
