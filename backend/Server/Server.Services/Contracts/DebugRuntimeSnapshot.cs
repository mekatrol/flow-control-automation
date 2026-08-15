namespace Server.Services.Contracts;

public sealed record DebugRuntimeSnapshot
{
    public required string DebugSessionId { get; init; }
    public required string FlowId { get; init; }
    public required uint Revision { get; init; }
    public required string LifecycleState { get; init; }
    public string Mode { get; init; } = "manual";
    public required ulong TickNumber { get; init; }
    public required ulong SampledAtMs { get; init; }
    public required ulong CompletedAtMs { get; init; }
    public required uint ExecutionDurationUs { get; init; }
    public uint ExecutionHighWaterUs { get; init; }
    public uint MissedDeadlineCount { get; init; }
    public IReadOnlyList<string> InputValidity { get; init; } = [];
    public IReadOnlyList<DebugNodeSnapshot> Nodes { get; init; } = [];
    public IReadOnlyList<DebugProposedOutput> ProposedOutputs { get; init; } = [];
    public uint OverrunCount { get; init; }
    public uint EvaluationFailureCount { get; init; }
    public uint ArbitrationLossCount { get; init; }
    public ushort LastReasonCode { get; init; }
    public required string LastReason { get; init; }
    public required string LastReasonPath { get; init; }
}