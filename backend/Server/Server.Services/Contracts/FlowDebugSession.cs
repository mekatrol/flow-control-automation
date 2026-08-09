namespace Server.Services.Contracts;

public sealed record FlowDebugSession
{
    public required string DebugSessionId { get; init; }
    public required string FlowId { get; init; }
    public required uint Revision { get; init; }
    public required string LifecycleState { get; init; }
    public required string Mode { get; init; }
    public ulong TickNumber { get; init; }
    public uint LeaseRemainingMilliseconds { get; init; }
    public ushort LastReasonCode { get; init; }
    public required string LastReason { get; init; }
    public required string LastReasonPath { get; init; }
    public DebugRuntimeSnapshot? Snapshot { get; init; }
    public IReadOnlyList<string> AffectedOutputPoints { get; init; } = [];
    public bool LiveOutputEnabled { get; init; }
    public byte? LiveOutputPriority { get; init; }
    public uint? LiveOutputHoldMilliseconds { get; init; }
}
