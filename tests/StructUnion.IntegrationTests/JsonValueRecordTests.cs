using System.Runtime.InteropServices;

namespace StructUnion.IntegrationTests.ComplexTypes;

[StructUnion]
public partial record JsonValueRecord
{
    public record Str(string Value);
    public record Num(double Value);
    public record Bool(bool Value);
    public record Arr(object[] Items);
    public record Obj(Dictionary<string, object> Data);
    public record Null();
}

public class JsonValueRecordTests
{
    [Test]
    public async Task JsonValue_String()
    {
        var v = JsonValue.Str("hello");
        await Assert.That(v.IsStr).IsTrue();
        await Assert.That(v.StrValue).IsEqualTo("hello");
    }

    [Test]
    public async Task JsonValue_Number()
    {
        var v = JsonValue.Num(3.14);
        await Assert.That(v.IsNum).IsTrue();
        await Assert.That(v.NumValue).IsEqualTo(3.14);
    }

    [Test]
    public async Task JsonValue_Bool()
    {
        var v = JsonValue.Bool(true);
        await Assert.That(v.IsBool).IsTrue();
        await Assert.That(v.BoolValue).IsTrue();
    }

    [Test]
    public async Task JsonValue_Array()
    {
        var items = new object[] { 1, "two", 3.0 };
        var v = JsonValue.Arr(items);
        await Assert.That(v.IsArr).IsTrue();
        await Assert.That(v.ArrItems).IsSameReferenceAs(items);
    }

    [Test]
    public async Task JsonValue_Dict()
    {
        var dict = new Dictionary<string, object> { ["key"] = "val" };
        var v = JsonValue.Obj(dict);
        await Assert.That(v.IsObj).IsTrue();
        await Assert.That(v.ObjData).IsSameReferenceAs(dict);
    }

    [Test]
    public async Task JsonValue_Null()
    {
        var v = JsonValue.Null();
        await Assert.That(v.IsNull).IsTrue();
        await Assert.That(v.TryGetNull()).IsTrue();
    }

    [Test]
    public async Task JsonValue_Match()
    {
        var v = JsonValue.Num(42);
        var result = v.Match(
            s => "str",
            n => $"num:{n}",
            b => "bool",
            a => "arr",
            d => "obj",
            () => "null");
        await Assert.That(result).IsEqualTo("num:42");
    }

    [Test]
    public async Task JsonValue_ExplicitLayout()
    {
        var layout = typeof(JsonValue).StructLayoutAttribute;
        await Assert.That(layout).IsNotNull();
        await Assert.That(layout!.Value).IsEqualTo(LayoutKind.Explicit);
    }
}
