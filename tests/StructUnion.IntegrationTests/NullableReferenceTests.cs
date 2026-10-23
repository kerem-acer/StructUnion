namespace StructUnion.IntegrationTests.NullableReferenceApi;

// This project builds with nullable enabled and warnings as errors, so a generated member that
// drops the `?` from rawBody fails the build (CS8611) before any test here runs.
[StructUnion]
public readonly partial struct StepMock
{
    public static partial StepMock Response(int statusCode, string body, string? rawBody);
    public static partial StepMock Note(string? text);
}

public class NullableReferenceTests
{
    [Test]
    public async Task Null_RoundTripsThroughAccessor()
    {
        var mock = StepMock.Response(204, "", null);
        await Assert.That(mock.ResponseRawBody).IsNull();
    }

    [Test]
    public async Task Null_RoundTripsThroughTryGet()
    {
        var mock = StepMock.Response(204, "", null);
        var found = mock.TryGetResponse(out var statusCode, out _, out var rawBody);
        await Assert.That(found).IsTrue();
        await Assert.That(statusCode).IsEqualTo(204);
        await Assert.That(rawBody).IsNull();
    }

    [Test]
    public async Task Null_ReachesMatch()
    {
        var mock = StepMock.Response(204, "", null);
        var result = mock.Match(
            (_, _, rawBody) => rawBody ?? "no body",
            text => text ?? "no text");
        await Assert.That(result).IsEqualTo("no body");
    }

    [Test]
    public async Task ImplicitConversion_AcceptsNull()
    {
        StepMock mock = (string?)null;
        await Assert.That(mock.IsNote).IsTrue();
        await Assert.That(mock.NoteText).IsNull();
    }

    [Test]
    public async Task NonNullValue_IsKept()
    {
        var mock = StepMock.Response(200, "ok", "{\"ok\":true}");
        await Assert.That(mock.ResponseRawBody).IsEqualTo("{\"ok\":true}");
    }
}
