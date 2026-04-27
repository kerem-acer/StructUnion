namespace StructUnion.IntegrationTests;

[StructUnion(GenerateDispose = true)]
public readonly partial struct Resource
{
    public static partial Resource File(MemoryStream stream);
    public static partial Resource Tagged(MemoryStream stream, string label);
    public static partial Resource Inline(int value);
}

[StructUnion(GenerateDispose = true)]
public readonly partial struct AsyncOnlyResource
{
    public static partial AsyncOnlyResource File(MemoryStream stream);
}

public class DisposableTests
{
    static MemoryStream MakeStream() => new([1, 2, 3]);

    [Test]
    public async Task UsingVar_DisposesActiveDisposableVariant()
    {
        var stream = MakeStream();
        using (var _ = Resource.File(stream))
        {
            await Assert.That(stream.CanRead).IsTrue();
        }
        await Assert.That(stream.CanRead).IsFalse();
    }

    [Test]
    public async Task UsingVar_NonDisposableVariant_IsNoOp()
    {
        // Inline carries no disposable; using should not throw and not mutate anything observable.
        var r = Resource.Inline(42);
        using (r)
        {
            // body
        }
        await Assert.That(r.IsInline).IsTrue();
        await Assert.That(r.InlineValue).IsEqualTo(42);
    }

    [Test]
    public async Task DefaultInstance_Dispose_DoesNotThrow()
    {
        var r = default(Resource);
        r.Dispose(); // must be safe — switch falls through Tags.Default
        await Assert.That(r.IsDefault).IsTrue();
    }

    [Test]
    public async Task TakeFile_TransfersOwnership_StreamRemainsOpen()
    {
        var stream = MakeStream();
        var r = Resource.File(stream);

        var taken = Resource.TakeFile(ref r);

        await Assert.That(ReferenceEquals(taken, stream)).IsTrue();
        await Assert.That(taken.CanRead).IsTrue();
        await Assert.That(r.IsDefault).IsTrue();

        // Subsequent Dispose on the now-default union must NOT dispose the taken stream.
        r.Dispose();
        await Assert.That(taken.CanRead).IsTrue();

        taken.Dispose();
    }

    [Test]
    public async Task TakeFile_WrongVariant_Throws()
    {
        var r = Resource.Inline(42);
        var threw = false;
        try
        {
            _ = Resource.TakeFile(ref r);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task TryTakeFile_RightVariant_ReturnsTrueAndZeroes()
    {
        var stream = MakeStream();
        var r = Resource.File(stream);

        var ok = Resource.TryTakeFile(ref r, out var taken);

        await Assert.That(ok).IsTrue();
        await Assert.That(ReferenceEquals(taken, stream)).IsTrue();
        await Assert.That(r.IsDefault).IsTrue();
        await Assert.That(taken.CanRead).IsTrue();

        taken.Dispose();
    }

    [Test]
    public async Task TryTakeFile_WrongVariant_ReturnsFalseAndDoesNotMutate()
    {
        var r = Resource.Inline(42);

        var ok = Resource.TryTakeFile(ref r, out var taken);

        await Assert.That(ok).IsFalse();
        await Assert.That(taken).IsNull();
        await Assert.That(r.IsInline).IsTrue();
        await Assert.That(r.InlineValue).IsEqualTo(42);
    }

    [Test]
    public async Task MultiFieldVariant_Take_WritesAllFieldsAndZeroes()
    {
        var stream = MakeStream();
        var r = Resource.Tagged(stream, "label-A");

        Resource.TakeTagged(ref r, out var takenStream, out var takenLabel);

        await Assert.That(ReferenceEquals(takenStream, stream)).IsTrue();
        await Assert.That(takenLabel).IsEqualTo("label-A");
        await Assert.That(r.IsDefault).IsTrue();
        await Assert.That(takenStream.CanRead).IsTrue();

        takenStream.Dispose();
    }

    [Test]
    public async Task DisposeAsync_AwaitUsing_DisposesAsync()
    {
        var stream = MakeStream();
        await using (var _ = AsyncOnlyResource.File(stream))
        {
            await Assert.That(stream.CanRead).IsTrue();
        }
        await Assert.That(stream.CanRead).IsFalse();
    }

    [Test]
    public async Task IDisposable_Interface_IsImplemented()
    {
        // Boxing is intentional for this check.
        object r = Resource.Inline(42);
        await Assert.That(r is IDisposable).IsTrue();
    }

    [Test]
    public async Task IAsyncDisposable_Interface_IsImplemented_WhenApplicable()
    {
        object r = AsyncOnlyResource.File(MakeStream());
        await Assert.That(r is IAsyncDisposable).IsTrue();

        // Clean up via the interface.
        await ((IAsyncDisposable)r).DisposeAsync();
    }
}
