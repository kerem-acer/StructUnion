namespace StructUnion.IntegrationTests.ComplexTypes;

[StructUnion]
public partial record EventRecord(Guid CorrelationId)
{
    public record UserCreated(string Name, int Age);
    public record UserDeleted(Guid UserId);
    public record StatusChanged(Status OldStatus, Status NewStatus);
    public record BatchCompleted(int Count, double Duration);
}

public class EventRecordTests
{
    [Test]
    public async Task Event_CommonGuid()
    {
        var id = Guid.NewGuid();
        var evt = Event.UserCreated(id, "alice", 30);
        await Assert.That(evt.CorrelationId).IsEqualTo(id);
        await Assert.That(evt.IsUserCreated).IsTrue();
        await Assert.That(evt.UserCreatedName).IsEqualTo("alice");
        await Assert.That(evt.UserCreatedAge).IsEqualTo(30);
    }

    [Test]
    public async Task Event_GuidVariant()
    {
        var corrId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = Event.UserDeleted(corrId, userId);
        await Assert.That(evt.CorrelationId).IsEqualTo(corrId);
        await Assert.That(evt.UserDeletedUserId).IsEqualTo(userId);
    }

    [Test]
    public async Task Event_EnumVariant()
    {
        var id = Guid.NewGuid();
        var evt = Event.StatusChanged(id, Status.Active, Status.Inactive);
        await Assert.That(evt.IsStatusChanged).IsTrue();
        await Assert.That(evt.StatusChangedOldStatus).IsEqualTo(Status.Active);
        await Assert.That(evt.StatusChangedNewStatus).IsEqualTo(Status.Inactive);
    }

    [Test]
    public async Task Event_MixedValueTypes()
    {
        var id = Guid.NewGuid();
        var evt = Event.BatchCompleted(id, 100, 3.5);
        await Assert.That(evt.BatchCompletedCount).IsEqualTo(100);
        await Assert.That(evt.BatchCompletedDuration).IsEqualTo(3.5);
    }

    [Test]
    public async Task Event_Equality_IncludesCommon()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var a = Event.UserCreated(id1, "alice", 30);
        var b = Event.UserCreated(id2, "alice", 30);

        await Assert.That(a == b).IsFalse(); // different correlation id
    }

    [Test]
    public async Task Event_Match()
    {
        var evt = Event.StatusChanged(Guid.Empty, Status.Pending, Status.Active);
        var result = evt.Match(
            (name, age) => "created",
            uid => "deleted",
            (old, @new) => $"{old}->{@new}",
            (count, dur) => "batch");
        await Assert.That(result).IsEqualTo("Pending->Active");
    }
}
