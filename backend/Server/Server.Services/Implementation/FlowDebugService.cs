using System.Globalization;

namespace Server.Services.Implementation;

public sealed class FlowDebugService(
    IFlowCompilationTargetResolver targetResolver,
    IFlowCompiler compiler,
    IControllerDebugTransport transport,
    FlowDebugSessionRegistry registry,
    IFlowVirtualMachineFactory? machines = null,
    IFlowPointAdapter? points = null,
    FlowEmulatorService? emulators = null) : IFlowDebugService
{
    private const int MaximumBreakpoints = 32;
    private const int MaximumInspectableSlots = 256;

    public Task<FlowDebugSession> StartAsync(
        StartFlowDebugSession request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Host switch
        {
            "controller" => StartAsync(request.Source, request.ReplaceExisting, cancellationToken),
            "server" or "emulator" => StartLocalAsync(request, cancellationToken),
            _ => throw new ControllerGatewayException("validation", "Debug host must be server, emulator, or controller.")
        };
    }

    public async Task<FlowDebugSession> StartAsync(
        ExecutableFlowSource source,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            if (registry.Local is not null && replaceExisting)
            {
                registry.Local.Dispose();
                registry.Local = null;
                registry.Session = null;
            }
            if (registry.Session is not null && !replaceExisting)
            {
                throw new ControllerGatewayException("busy", "A debug session already exists.");
            }
            var target = await targetResolver.ResolveAsync(source, cancellationToken);
            var compilation = compiler.Compile(new FlowCompilationRequest
            {
                Source = source,
                Target = target
            });
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
        if (registry.Local is not null)
        {
            return MatchLocal(flowId, sessionId);
        }

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
        if (registry.Local is not null)
        {
            var session = await StepLocalTickAsync(flowId, sessionId, cancellationToken);
            return ToCompatibilitySnapshot(session);
        }
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
            if (registry.Local is not null)
            {
                MatchLocal(flowId, sessionId);
                registry.Local.Dispose();
                registry.Local = null;
                registry.Session = null;
                return;
            }
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
        if (registry.Local is not null)
        {
            var local = MatchLocal(flowId, sessionId);
            return registry.Session = local with { LifecycleState = "running", Mode = "interval" };
        }
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
        if (registry.Local is not null)
        {
            var local = MatchLocal(flowId, sessionId);
            return registry.Session = local with { LifecycleState = "paused", Mode = "manual" };
        }
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

    public async Task<FlowDebugSession> StepInstructionAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var local = GetLocal(flowId, sessionId);
            await EnsureFrameAsync(local, cancellationToken);
            if (!local.Frame!.IsAtCommit)
            {
                local.Frame = local.Machine.StepInstruction();
            }

            return UpdateLocalSession(local, "paused");
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    public async Task<FlowDebugSession> StepNodeAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var local = GetLocal(flowId, sessionId);
            await EnsureFrameAsync(local, cancellationToken);
            var initialNode = NodeAt(local, local.Frame!.InstructionIndex);
            do
            {
                if (local.Frame.IsAtCommit)
                {
                    break;
                }

                local.Frame = local.Machine.StepInstruction();
            }
            while (string.Equals(initialNode, NodeAt(local, local.Frame.InstructionIndex), StringComparison.Ordinal));
            return UpdateLocalSession(local, "paused");
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    public async Task<FlowDebugSession> RunToAsync(
        string flowId,
        string sessionId,
        FlowDebugBreakpoint breakpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var local = GetLocal(flowId, sessionId);
            ValidateBreakpoint(local, breakpoint);
            await EnsureFrameAsync(local, cancellationToken);
            while (!local.Frame!.IsAtCommit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.Equals(NodeAt(local, local.Frame.InstructionIndex), breakpoint.NodeId, StringComparison.Ordinal))
                {
                    break;
                }

                local.Frame = local.Machine.StepInstruction();
            }
            return UpdateLocalSession(local, "paused");
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    public async Task<FlowDebugSession> ReplaceBreakpointsAsync(
        string flowId,
        string sessionId,
        IReadOnlyList<FlowDebugBreakpoint> breakpoints,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var local = GetLocal(flowId, sessionId);
            if (breakpoints.Count > MaximumBreakpoints)
            {
                throw new ControllerGatewayException("validation", "Breakpoint capacity was exceeded.");
            }
            foreach (var breakpoint in breakpoints)
            {
                ValidateBreakpoint(local, breakpoint);
            }

            local.Breakpoints = [.. breakpoints];
            return UpdateLocalSession(local, registry.Session!.LifecycleState);
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    public Task<FlowDebugInspection> InspectAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var local = GetLocal(flowId, sessionId);
        if (local.Frame is null)
        {
            throw new ControllerGatewayException("validation", "No paused execution frame is available.");
        }
        return Task.FromResult(ToInspection(local));
    }

    public async Task<FlowDebugSession> RestartAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var local = GetLocal(flowId, sessionId);
            if (local.Frame is not null)
            {
                local.Machine.AbortScan();
            }

            local.Frame = null;
            local.Machine.Reset();
            return UpdateLocalSession(local, "ready") with { TickNumber = 0, Snapshot = null };
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    private static IReadOnlyList<string> GetAffectedOutputPoints(ExecutableFlowSource source) =>
        [.. source.Nodes
            .Where(node => string.Equals(node.Kind, "digitalOutput", StringComparison.Ordinal))
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .Select(node => node.Configuration.TryGetValue("pointId", out var pointId) ? pointId.GetString() : null)
            .Where(pointId => !string.IsNullOrEmpty(pointId))
            .Cast<string>()];

    private async Task<FlowDebugSession> StartLocalAsync(
        StartFlowDebugSession request,
        CancellationToken cancellationToken)
    {
        if (machines is null || points is null || emulators is null)
        {
            throw new ControllerGatewayException("unavailable", "Local debug hosts are unavailable.");
        }
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            if (registry.Session is not null && !request.ReplaceExisting)
            {
                throw new ControllerGatewayException("busy", "A debug session already exists.");
            }
            if (registry.Session is { Host: "controller" } controller && request.ReplaceExisting
                && ulong.TryParse(controller.DebugSessionId, NumberStyles.None, CultureInfo.InvariantCulture, out var controllerId))
            {
                await transport.StopAsync(controllerId, cancellationToken);
                registry.Session = null;
            }
            registry.Local?.Dispose();
            registry.Local = null;
            var target = await targetResolver.ResolveAsync(request.Source, cancellationToken);
            var compilation = compiler.Compile(new FlowCompilationRequest { Source = request.Source, Target = target });
            FlowEmulatorService.Instance? emulator = null;
            if (request.Host == "emulator")
            {
                if (string.IsNullOrWhiteSpace(request.EmulatorId))
                {
                    throw new ControllerGatewayException("validation", "An emulator ID is required for the emulator host.");
                }
                emulator = emulators.GetInstance(request.EmulatorId);
                if (!string.Equals(emulator.Snapshot().FlowId, request.Source.Id, StringComparison.Ordinal))
                {
                    throw new ControllerGatewayException("validation", "The emulator flow does not match the debug source.");
                }
            }
            var sessionId = Guid.NewGuid().ToString("N");
            var local = new LocalFlowDebugSession(
                machines.Create(compilation.Artifact), request.Source, compilation, request.Host, sessionId, emulator);
            registry.Local = local;
            return registry.Session = LocalSession(local, "ready");
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    private async Task<FlowDebugSession> StepLocalTickAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await registry.Gate.WaitAsync(cancellationToken);
        try
        {
            var local = GetLocal(flowId, sessionId);
            await EnsureFrameAsync(local, cancellationToken);
            var scan = local.Machine.CommitScan();
            local.Frame = null;
            if (local.Emulator is not null)
            {
                local.Emulator.Publish(scan);
            }
            else
            {
                await points!.PublishAsync(local.Source.Id, scan.Commands, cancellationToken);
            }
            var updated = UpdateLocalSession(local, "paused") with
            {
                TickNumber = scan.ScanNumber,
                Snapshot = CompatibilitySnapshot(local, scan)
            };
            registry.Session = updated;
            return updated;
        }
        finally
        {
            registry.Gate.Release();
        }
    }

    private async Task EnsureFrameAsync(LocalFlowDebugSession local, CancellationToken cancellationToken)
    {
        if (local.Frame is not null)
        {
            return;
        }

        IReadOnlyList<FlowVmInput> inputs;
        ulong sampledAt;
        if (local.Emulator is not null)
        {
            inputs = local.Emulator.CaptureInputs();
            sampledAt = local.Emulator.Clock;
        }
        else
        {
            var ids = local.Source.Nodes
                .Where(node => node.Kind == "digitalInput")
                .Select(node => node.Configuration["pointId"].GetString()!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            inputs = await points!.ReadAsync(ids, cancellationToken);
            sampledAt = checked((ulong)Environment.TickCount64);
        }
        local.Frame = local.Machine.BeginScan(inputs, sampledAt);
    }

    private FlowDebugSession MatchLocal(string flowId, string sessionId)
    {
        GetLocal(flowId, sessionId);
        return registry.Session!;
    }

    private LocalFlowDebugSession GetLocal(string flowId, string sessionId)
    {
        if (registry.Local is not { } local
            || !string.Equals(local.Source.Id, flowId, StringComparison.Ordinal)
            || !string.Equals(local.SessionId, sessionId, StringComparison.Ordinal))
        {
            throw new FlowDebugSessionNotFoundException(sessionId);
        }
        return local;
    }

    private FlowDebugSession UpdateLocalSession(LocalFlowDebugSession local, string state)
    {
        var current = registry.Session!;
        return registry.Session = LocalSession(local, state) with
        {
            TickNumber = current.TickNumber,
            Snapshot = current.Snapshot,
            Breakpoints = local.Breakpoints,
            Inspection = local.Frame is null ? null : ToInspection(local)
        };
    }

    private static FlowDebugSession LocalSession(LocalFlowDebugSession local, string state) => new()
    {
        DebugSessionId = local.SessionId,
        FlowId = local.Source.Id,
        Revision = local.Source.Revision,
        LifecycleState = state,
        Mode = "manual",
        LastReason = "ok",
        LastReasonPath = string.Empty,
        Host = local.Host,
        AffectedOutputPoints = GetAffectedOutputPoints(local.Source),
        Capabilities = new FlowDebugCapabilities
        {
            StepTick = true,
            StepNode = true,
            StepInstruction = true,
            Continue = true,
            Pause = true,
            RunTo = true,
            MaximumBreakpoints = MaximumBreakpoints,
            MaximumInspectableSlots = MaximumInspectableSlots
        },
        SourceDigest = local.Compilation.ArtifactSha256,
        ExecutionOrder = local.Compilation.Schedule
    };

    private static FlowDebugInspection ToInspection(LocalFlowDebugSession local)
    {
        var frame = local.Frame!;
        return new FlowDebugInspection
        {
            InstructionPointer = frame.InstructionIndex,
            IsAtCommit = frame.IsAtCommit,
            NodeId = NodeAt(local, frame.InstructionIndex),
            Slots = [.. frame.Slots.Select(DebugValue)],
            CurrentState = [.. frame.CurrentState.Select(value => new DebugTypedValue(DataType.Boolean, Value: value))],
            StagedNextState = [.. frame.StagedState.Select(value => value.HasValue ? new DebugTypedValue(DataType.Boolean, Value: value.Value) : null)],
            ProposedOutputs = frame.ProposedCommands,
            NodeValues = local.Compilation.NodeIndices
                .Where(pair => pair.Value < frame.Slots.Count)
                .ToDictionary(pair => pair.Key, pair => DebugValue(frame.Slots[pair.Value]), StringComparer.Ordinal)
        };
    }

    private static string? NodeAt(LocalFlowDebugSession local, ushort instructionIndex) =>
        instructionIndex < local.Compilation.Schedule.Count ? local.Compilation.Schedule[instructionIndex] : null;

    private static void ValidateBreakpoint(LocalFlowDebugSession local, FlowDebugBreakpoint breakpoint)
    {
        if (breakpoint.Position is not ("before" or "after")
            || !local.Compilation.Schedule.Contains(breakpoint.NodeId, StringComparer.Ordinal))
        {
            throw new ControllerGatewayException("validation", "Breakpoint does not resolve in this flow revision.");
        }
    }

    private static DebugRuntimeSnapshot CompatibilitySnapshot(LocalFlowDebugSession local, FlowVmScanResult scan) => new()
    {
        DebugSessionId = local.SessionId,
        FlowId = local.Source.Id,
        Revision = local.Source.Revision,
        LifecycleState = "paused",
        TickNumber = scan.ScanNumber,
        SampledAtMs = scan.SampledAtMilliseconds,
        CompletedAtMs = scan.SampledAtMilliseconds,
        ExecutionDurationUs = 0,
        LastReason = "ok",
        LastReasonPath = string.Empty,
        Nodes = [.. local.Compilation.NodeIndices
            .OrderBy(pair => pair.Value)
            .Select(pair => new DebugNodeSnapshot(
                pair.Key,
                "evaluated",
                DataQuality.Good,
                DebugValue(scan.Slots[pair.Value])))],
        ProposedOutputs = [.. scan.Commands.Select(command => new DebugProposedOutput(
            command.PointId,
            "proposed",
            command.TypedValue.Quality,
            command.Value,
            command.TypedValue.DataType == DataType.Number ? command.TypedValue.Number : null,
            command.TypedValue))]
    };

    private static DebugTypedValue DebugValue(FlowVmValue value) => value.DataType == DataType.Number
        ? new DebugTypedValue(DataType.Number, Number: value.Number, Quality: value.Quality)
        : new DebugTypedValue(DataType.Boolean, Value: value.Boolean, Quality: value.Quality);

    private static DebugRuntimeSnapshot ToCompatibilitySnapshot(FlowDebugSession session) => session.Snapshot
        ?? throw new InvalidOperationException("The completed scan did not produce a snapshot.");

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