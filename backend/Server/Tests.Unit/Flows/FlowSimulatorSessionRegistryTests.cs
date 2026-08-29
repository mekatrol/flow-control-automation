using Server.Services.Implementation;

namespace Tests.Unit.Flows;

public sealed class FlowSimulatorSessionRegistryTests
{
    [Test]
    public async Task InactiveSessionExpiresAndRunsCleanupWithoutAnotherRegistryRequest()
    {
        var cleaned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sessions = new FlowSimulatorSessionRegistry(TimeProvider.System, TimeSpan.FromMilliseconds(50));
        sessions.Add("flow-a", new FlowDebugSessionRegistry(), false, cleanup: () => cleaned.TrySetResult());

        await cleaned.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(sessions.Get("flow-a"), Is.Null);
    }

    [Test]
    public async Task TouchRenewsTheSessionLease()
    {
        var cleaned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sessions = new FlowSimulatorSessionRegistry(TimeProvider.System, TimeSpan.FromMilliseconds(300));
        var entry = sessions.Add(
            "flow-a",
            new FlowDebugSessionRegistry(),
            false,
            cleanup: () => cleaned.TrySetResult());

        await Task.Delay(200);
        sessions.Touch(entry);
        await Task.Delay(200);

        Assert.That(sessions.Get("flow-a"), Is.SameAs(entry));
        await cleaned.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public void ClearReleasesEveryActiveSession()
    {
        var cleaned = 0;
        using var sessions = new FlowSimulatorSessionRegistry(TimeProvider.System, TimeSpan.FromMinutes(1));
        sessions.Add("flow-a", new FlowDebugSessionRegistry(), false, cleanup: () => cleaned++);
        sessions.Add("flow-b", new FlowDebugSessionRegistry(), false, cleanup: () => cleaned++);

        sessions.Clear();

        Assert.That(cleaned, Is.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(sessions.Get("flow-a"), Is.Null);
            Assert.That(sessions.Get("flow-b"), Is.Null);
        });
    }
}