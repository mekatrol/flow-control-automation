using Server.Common.Contracts;
using Server.Common.Services;
using Server.Compiler.Services;
using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text;
using System.Text.Json;

namespace Tests.Unit.Flows;

public sealed class FlowDebugServiceTests
{
    [Test]
    public async Task OrchestratesCompileLoadPrepareStepInspectAndStop()
    {
        var transport = new StubTransport(Snapshot());
        var service = new FlowDebugService(
            new StubResolver(),
            new StubCompiler(),
            transport,
            new FlowDebugSessionRegistry());
        var source = Source();

        var started = await service.StartAsync(source, replaceExisting: false, default);
        var stepped = await service.StepAsync(source.Id, started.DebugSessionId, default);
        var inspected = await service.GetAsync(source.Id, started.DebugSessionId, default);
        await service.StopAsync(source.Id, started.DebugSessionId, default);

        Assert.Multiple(() =>
        {
            Assert.That(started.LifecycleState, Is.EqualTo("ready"));
            Assert.That(stepped.TickNumber, Is.EqualTo(1));
            Assert.That(inspected.Snapshot, Is.SameAs(stepped));
            Assert.That(transport.Calls,
                Is.EqualTo(new[] { "load", "prepare", "step", "status", "stop" }));
        });
    }

    [Test]
    public void RejectsStaleApplicationSessionBeforeTransport()
    {
        var transport = new StubTransport(Snapshot());
        var service = new FlowDebugService(
            new StubResolver(),
            new StubCompiler(),
            transport,
            new FlowDebugSessionRegistry());

        Assert.That(
            async () => await service.GetAsync("flow-a", "42", default),
            Throws.TypeOf<FlowDebugSessionNotFoundException>());
        Assert.That(transport.Calls, Is.Empty);
    }

    [Test]
    public async Task RequiresExactOutputConfirmationBeforeEnablingLiveOutput()
    {
        var transport = new StubTransport(Snapshot());
        var service = new FlowDebugService(new StubResolver(), new StubCompiler(), transport, new FlowDebugSessionRegistry());
        var source = Source();
        var started = await service.StartAsync(source, false, default);

        Assert.That(
            async () => await service.EnableLiveOutputAsync(source.Id, started.DebugSessionId, ["output-02"], default),
            Throws.TypeOf<ControllerGatewayException>());
        var enabled = await service.EnableLiveOutputAsync(source.Id, started.DebugSessionId, ["output-01"], default);

        Assert.Multiple(() =>
        {
            Assert.That(enabled.LiveOutputEnabled, Is.True);
            Assert.That(enabled.LiveOutputPriority, Is.EqualTo(8));
            Assert.That(enabled.LiveOutputHoldMilliseconds, Is.EqualTo(1000));
            Assert.That(transport.Calls, Does.Contain("live"));
        });
    }

    private static ExecutableFlowSource Source() => new()
    {
        Id = "flow-a",
        Revision = 3,
        ControllerTemplateId = "controller-a",
        ControllerTemplateRevision = 1,
        Nodes =
        [
            new ExecutableFlowNode
            {
                Id = "constant",
                Kind = FlowNodeKind.DigitalConstant
            },
            new ExecutableFlowNode
            {
                Id = "output",
                Kind = FlowNodeKind.DigitalOutput,
                Configuration = new Dictionary<string, JsonElement>
                {
                    ["pointId"] = JsonSerializer.SerializeToElement("output-01")
                }
            }
        ]
    };

    private static byte[] Snapshot()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)1);
        writer.Write((ulong)42);
        writer.Write((byte)6);
        writer.Write(Encoding.UTF8.GetBytes("flow-a"));
        writer.Write((uint)3);
        writer.Write((byte)4);
        writer.Write((byte)1);
        writer.Write((ulong)1);
        writer.Write((ulong)1000);
        writer.Write((ulong)1001);
        writer.Write((uint)1);
        writer.Write((byte)7);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((uint)0);
        writer.Write((uint)0);
        writer.Write((ushort)0);
        writer.Write((byte)0);
        writer.Write((uint)1);
        writer.Write((uint)0);
        writer.Write((uint)0);
        return stream.ToArray();
    }

    private sealed class StubResolver : IFlowCompilationTargetResolver
    {
        public Task<FlowCompilationTarget> ResolveAsync(
            ExecutableFlowSource source,
            CancellationToken cancellationToken) => Task.FromResult(new FlowCompilationTarget
            {
                ControllerTemplate = null!
            });
    }

    private sealed class StubCompiler : IFlowCompiler
    {
        public FlowCompilationResult Compile(FlowCompilationRequest request) => new()
        {
            Artifact = new byte[] { 1, 2, 3 },
            ArtifactSha256 = "fixture",
            FlowRevision = request.Source.Revision,
            ControllerTemplateId = request.Source.ControllerTemplateId,
            ControllerTemplateRevision = checked((int)request.Source.ControllerTemplateRevision)
        };

        public void WriteBinary(FlowCompilationResult compilation, string path)
        {
            throw new NotImplementedException();
        }

        public void WriteIntelHex(FlowCompilationResult compilation, string path, uint baseAddress = 0, int bytesPerRecord = 16)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class StubTransport(byte[] snapshot) : IControllerDebugTransport
    {
        public List<string> Calls { get; } = [];

        public Task<ControllerDebugLoadResult> LoadAsync(
            ReadOnlyMemory<byte> artifact,
            bool replaceExisting,
            CancellationToken cancellationToken)
        {
            Calls.Add("load");
            return Task.FromResult(new ControllerDebugLoadResult(42, 180, 30000));
        }

        public Task<ControllerDebugWireStatus> PrepareAsync(
            ulong sessionId,
            CancellationToken cancellationToken)
        {
            Calls.Add("prepare");
            return Task.FromResult(Status(state: 2, tick: 0));
        }

        public Task<ControllerDebugWireStatus> GetStatusAsync(
            ulong sessionId,
            CancellationToken cancellationToken)
        {
            Calls.Add("status");
            return Task.FromResult(Status(state: 4, tick: 1));
        }

        public Task<ControllerDebugSnapshotEnvelope> StepAsync(
            ulong sessionId,
            CancellationToken cancellationToken)
        {
            Calls.Add("step");
            return Task.FromResult(new ControllerDebugSnapshotEnvelope(42, 1, snapshot, new byte[32]));
        }

        public Task RenewLeaseAsync(ulong sessionId, CancellationToken cancellationToken)
        {
            Calls.Add("renew");
            return Task.CompletedTask;
        }

        public Task<ControllerDebugWireStatus> RunAsync(ulong sessionId, uint intervalMilliseconds, CancellationToken cancellationToken) =>
            Task.FromResult(Status(state: 7, tick: 1));

        public Task<ControllerDebugWireStatus> PauseAsync(ulong sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Status(state: 4, tick: 1));

        public Task<ControllerDebugLiveOutputResult> EnableLiveOutputAsync(
            ulong sessionId, IReadOnlyList<string> confirmedPointIds, CancellationToken cancellationToken)
        {
            Calls.Add("live");
            return Task.FromResult(new ControllerDebugLiveOutputResult(8, 1000));
        }

        public Task<ControllerDebugSnapshotEnvelope> ReadSnapshotAsync(ulong sessionId, ulong tickNumber, CancellationToken cancellationToken) =>
            Task.FromResult(new ControllerDebugSnapshotEnvelope(42, tickNumber, snapshot, new byte[32]));

        public Task StopAsync(ulong sessionId, CancellationToken cancellationToken)
        {
            Calls.Add("stop");
            return Task.CompletedTask;
        }

        private static ControllerDebugWireStatus Status(byte state, ulong tick) =>
            new(42, state, 3, 3, 3, tick, 30000, 0, string.Empty);
    }
}