using System.Text.Json;

namespace Tests.Unit.FlowExecution.ExecutionContext;

public sealed class FlowExecutionContractTests
{
    [Test]
    public void CapabilityProfilesExposeCoreOperationsInEveryMode()
    {
        var profiles = new[]
        {
            FlowExecutionCapabilities.Simulator,
            FlowExecutionCapabilities.ServerDebugger,
            FlowExecutionCapabilities.ControllerDebugger(false)
        };

        Assert.Multiple(() =>
        {
            Assert.That(profiles, Has.All.Property(nameof(FlowExecutionCapabilities.CanRun)).True);
            Assert.That(profiles, Has.All.Property(nameof(FlowExecutionCapabilities.CanPause)).True);
            Assert.That(profiles, Has.All.Property(nameof(FlowExecutionCapabilities.CanStop)).True);
            Assert.That(profiles, Has.All.Property(nameof(FlowExecutionCapabilities.CanRestart)).True);
            Assert.That(profiles, Has.All.Property(nameof(FlowExecutionCapabilities.CanStepTick)).True);
            Assert.That(profiles, Has.All.Property(nameof(FlowExecutionCapabilities.CanStepNode)).True);
            Assert.That(profiles, Has.All.Property(nameof(FlowExecutionCapabilities.CanStepInstruction)).True);
            Assert.That(profiles, Has.All.Property(nameof(FlowExecutionCapabilities.CanUseBreakpoints)).True);
            Assert.That(profiles, Has.All.Property(nameof(FlowExecutionCapabilities.CanRunTo)).True);
        });
    }

    [Test]
    public void CapabilityProfilesExposeOnlyHostSpecificOptionalOperations()
    {
        var simulator = FlowExecutionCapabilities.Simulator;
        var server = FlowExecutionCapabilities.ServerDebugger;
        var controller = FlowExecutionCapabilities.ControllerDebugger(true);

        Assert.Multiple(() =>
        {
            Assert.That(simulator.CanEditInputs, Is.True);
            Assert.That(simulator.CanAdvanceVirtualTime, Is.True);
            Assert.That(simulator.CanInjectFaults, Is.True);
            Assert.That(simulator.CanResetIo, Is.True);
            Assert.That(simulator.CanEnableLiveOutputs, Is.False);
            Assert.That(simulator.LocksFlowEditing, Is.False);
            Assert.That(server.CanEditInputs, Is.False);
            Assert.That(server.CanEnableLiveOutputs, Is.False);
            Assert.That(server.LocksFlowEditing, Is.True);
            Assert.That(controller.CanEditInputs, Is.False);
            Assert.That(controller.CanEnableLiveOutputs, Is.True);
            Assert.That(controller.LocksFlowEditing, Is.True);
        });
    }

    [TestCase(FlowExecutionMode.Simulator, "simulator")]
    [TestCase(FlowExecutionMode.Debugger, "debugger")]
    public void ContextEnvelopeSerializesModesThroughOneCanonicalShape(FlowExecutionMode mode, string expectedMode)
    {
        var context = CreateContext(mode);
        var json = JsonSerializer.Serialize(context, FlowControlJson.Options);
        var roundTrip = JsonSerializer.Deserialize<FlowExecutionContext>(json, FlowControlJson.Options);
        using var document = JsonDocument.Parse(json);

        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("mode").GetString(), Is.EqualTo(expectedMode));
            Assert.That(document.RootElement.GetProperty("lifecycle").GetString(), Is.EqualTo("ready"));
            Assert.That(document.RootElement.TryGetProperty("debugSessionId", out _), Is.False);
            Assert.That(document.RootElement.TryGetProperty("sessionId", out _), Is.False);
            Assert.That(roundTrip!.Id, Is.EqualTo(context.Id));
            Assert.That(roundTrip.FlowId, Is.EqualTo(context.FlowId));
            Assert.That(roundTrip.Mode, Is.EqualTo(context.Mode));
            Assert.That(roundTrip.Lifecycle, Is.EqualTo(context.Lifecycle));
            Assert.That(roundTrip.Capabilities, Is.EqualTo(context.Capabilities));
            Assert.That(roundTrip.Breakpoints, Is.EqualTo(context.Breakpoints));
        });
    }

    [Test]
    public void CreateRequestRejectsMissingRequiredIdentity()
    {
        const string missingFlowId = "{\"mode\":\"simulator\",\"expectedRevision\":1,\"targetId\":\"server\"}";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CreateFlowExecutionContext>(missingFlowId, FlowControlJson.Options));
    }

    [Test]
    public void CreateRequestRejectsUnknownMembers()
    {
        const string legacySource = "{\"flowId\":\"flow-a\",\"mode\":\"simulator\",\"expectedRevision\":1,\"targetId\":\"server\",\"source\":{}}";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CreateFlowExecutionContext>(legacySource, FlowControlJson.Options));
    }

    private static FlowExecutionContext CreateContext(FlowExecutionMode mode) => new()
    {
        Id = "context-a",
        FlowId = "flow-a",
        Revision = 3,
        Mode = mode,
        Lifecycle = FlowExecutionLifecycle.Ready,
        Capabilities = mode == FlowExecutionMode.Simulator
            ? FlowExecutionCapabilities.Simulator
            : FlowExecutionCapabilities.ServerDebugger,
        Breakpoints = [new FlowDebugBreakpoint("node-a")],
        Presentation = new FlowExecutionPresentation
        {
            ModeLabel = mode.ToString(),
            HostLabel = "Server",
            IsSimulated = mode == FlowExecutionMode.Simulator
        },
        LeaseRemainingMilliseconds = 30_000
    };
}