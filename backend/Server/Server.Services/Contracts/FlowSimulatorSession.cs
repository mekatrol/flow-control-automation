namespace Server.Services.Contracts;

public sealed record FlowSimulatorSession
{
    public required string SessionId { get; init; }
    public required string FlowId { get; init; }
    public required uint SourceRevision { get; init; }
    public required string SourceDigest { get; init; }
    public required string LifecycleState { get; init; }
    public required FlowDebugCapabilities Capabilities { get; init; }
    public DebugRuntimeSnapshot? Snapshot { get; init; }
    public FlowEmulatorSnapshot? Io { get; init; }
    public FlowDebugInspection? Inspection { get; init; }
    public IReadOnlyList<FlowDebugBreakpoint> Breakpoints { get; init; } = [];
    public uint LeaseRemainingMilliseconds { get; init; }
}