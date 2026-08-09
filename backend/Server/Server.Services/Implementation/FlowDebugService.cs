using Server.Services.Contracts;
using System.Globalization;

namespace Server.Services.Implementation;

public sealed class FlowDebugService(
    IFlowCompilationTargetResolver targetResolver,
    IFlowCompiler compiler,
    IControllerDebugTransport transport,
    FlowDebugSessionRegistry registry) : IFlowDebugService
{
    public async Task<FlowDebugSession> StartAsync(
        ExecutableFlowSource source,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            if (registry.Session is not null && !replaceExisting)
            {
                throw new ControllerGatewayException("busy", "A debug session already exists.");
            }
            var target = await targetResolver.ResolveAsync(source, cancellationToken);
            var compilation = compiler.Compile(new FlowCompilationRequest { Source = source, Target = target });
            var load = await transport.LoadAsync(compilation.Artifact, replaceExisting, cancellationToken);
            try
            {
                var status = await transport.PrepareAsync(load.SessionId, cancellationToken);
                ValidateStatus(status, load.SessionId, source.Revision);
                var session = ToSession(source.Id, status, snapshot: null);
                registry.Session = session with { AffectedOutputPoints = GetAffectedOutputPoints(source) };
                return registry.Session;
            }
            catch
            {
                Exception? cleanupFailure = null;
                try
                {
                    await transport.StopAsync(load.SessionId, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    cleanupFailure = exception;
                }
                _ = cleanupFailure;
                throw;
            }
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    public async Task<FlowDebugSession> GetAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var id = ParseAndMatch(flowId, sessionId);
        var status = await transport.GetStatusAsync(id, cancellationToken);
        ValidateStatus(status, id, registry.Session!.Revision);
        var snapshot = registry.Session.Snapshot;
        if (status.TickNumber > 0 && (snapshot is null || snapshot.TickNumber != status.TickNumber))
        {
            snapshot = DebugSnapshotDecoder.Decode(await transport.ReadSnapshotAsync(id, status.TickNumber, cancellationToken));
        }
        var updated = CopyLiveOutputState(ToSession(flowId, status, snapshot), registry.Session);
        registry.Session = updated;
        return updated;
    }

    public async Task<DebugRuntimeSnapshot> StepAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var id = ParseAndMatch(flowId, sessionId);
            var envelope = await transport.StepAsync(id, cancellationToken);
            var snapshot = DebugSnapshotDecoder.Decode(envelope);
            if (snapshot.FlowId != flowId || snapshot.Revision != registry.Session!.Revision
                || snapshot.DebugSessionId != sessionId)
            {
                throw new ControllerGatewayException("stale_session", "Snapshot identity does not match the active flow.");
            }
            registry.Session = registry.Session with
            {
                LifecycleState = snapshot.LifecycleState,
                TickNumber = snapshot.TickNumber,
                LastReasonCode = snapshot.LastReasonCode,
                LastReason = snapshot.LastReason,
                LastReasonPath = snapshot.LastReasonPath,
                Snapshot = snapshot
            };
            return snapshot;
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    public async Task StopAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var id = ParseAndMatch(flowId, sessionId);
            await transport.StopAsync(id, cancellationToken);
            registry.Session = null;
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    public async Task<FlowDebugSession> RunAsync(
        string flowId, string sessionId, uint intervalMilliseconds, CancellationToken cancellationToken)
    {
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var id = ParseAndMatch(flowId, sessionId);
            var status = await transport.RunAsync(id, intervalMilliseconds, cancellationToken);
            ValidateStatus(status, id, registry.Session!.Revision);
            return registry.Session = CopyLiveOutputState(ToSession(flowId, status, registry.Session.Snapshot), registry.Session);
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    public async Task<FlowDebugSession> PauseAsync(
        string flowId, string sessionId, CancellationToken cancellationToken)
    {
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var id = ParseAndMatch(flowId, sessionId);
            var status = await transport.PauseAsync(id, cancellationToken);
            ValidateStatus(status, id, registry.Session!.Revision);
            return registry.Session = CopyLiveOutputState(ToSession(flowId, status, registry.Session.Snapshot), registry.Session);
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    public async Task<FlowDebugSession> EnableLiveOutputAsync(
        string flowId,
        string sessionId,
        IReadOnlyList<string> confirmedPointIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(confirmedPointIds);
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var id = ParseAndMatch(flowId, sessionId);
            var session = registry.Session!;
            if (!confirmedPointIds.SequenceEqual(session.AffectedOutputPoints, StringComparer.Ordinal))
            {
                throw new ControllerGatewayException(
                    "validation",
                    "Live-output confirmation must exactly match the affected output points.");
            }
            var policy = await transport.EnableLiveOutputAsync(id, confirmedPointIds, cancellationToken);
            return registry.Session = session with
            {
                LiveOutputEnabled = true,
                LiveOutputPriority = policy.Priority,
                LiveOutputHoldMilliseconds = policy.HoldMilliseconds
            };
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    private static IReadOnlyList<string> GetAffectedOutputPoints(ExecutableFlowSource source) =>
        source.Nodes
            .Where(node => string.Equals(node.Kind, "digitalOutput", StringComparison.Ordinal))
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .Select(node => node.Configuration.TryGetValue("pointId", out var pointId) ? pointId.GetString() : null)
            .Where(pointId => !string.IsNullOrEmpty(pointId))
            .Cast<string>()
            .ToArray();

    private static FlowDebugSession CopyLiveOutputState(FlowDebugSession next, FlowDebugSession current) => next with
    {
        AffectedOutputPoints = current.AffectedOutputPoints,
        LiveOutputEnabled = current.LiveOutputEnabled,
        LiveOutputPriority = current.LiveOutputPriority,
        LiveOutputHoldMilliseconds = current.LiveOutputHoldMilliseconds
    };

    private ulong ParseAndMatch(string flowId, string sessionId)
    {
        if (!ulong.TryParse(sessionId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || id == 0
            || registry.Session is not { } active
            || active.FlowId != flowId
            || active.DebugSessionId != sessionId)
        {
            throw new FlowDebugSessionNotFoundException(sessionId);
        }
        return id;
    }

    private static void ValidateStatus(ControllerDebugWireStatus status, ulong sessionId, uint revision)
    {
        if (status.SessionId != sessionId || status.FlowRevision != revision || status.State > 7)
        {
            throw new ControllerGatewayException("protocol", "Controller status identity is inconsistent.");
        }
    }

    private static FlowDebugSession ToSession(
        string flowId,
        ControllerDebugWireStatus status,
        DebugRuntimeSnapshot? snapshot) => new()
        {
            DebugSessionId = status.SessionId.ToString(CultureInfo.InvariantCulture),
            FlowId = flowId,
            Revision = status.FlowRevision,
            LifecycleState = StateNames[status.State],
            Mode = "manual",
            TickNumber = status.TickNumber,
            LeaseRemainingMilliseconds = status.LeaseRemainingMilliseconds,
            LastReasonCode = status.LastReasonCode,
            LastReason = ReasonName(status.LastReasonCode),
            LastReasonPath = status.LastReasonPath,
            Snapshot = snapshot
        };

    private static readonly string[] StateNames =
        ["empty", "loading", "ready", "stepping", "paused", "fault", "stopped", "running"];
    private static readonly string[] ReasonNames =
    [
        "ok", "malformed", "unsupported_schema", "length_mismatch", "digest_mismatch", "limit_exceeded",
        "invalid_identifier", "non_canonical_order", "unknown_node_kind", "invalid_configuration",
        "invalid_port_shape", "missing_connection", "duplicate_driver", "incompatible_type", "missing_point",
        "point_direction_mismatch", "combinational_cycle", "unsupported_mode", "unsupported_capability",
        "snapshot_too_large", "input_quality_rejected", "evaluation_failed"
    ];

    private static string ReasonName(ushort reason) =>
        reason < ReasonNames.Length ? ReasonNames[reason] : $"unknown_{reason}";
}
