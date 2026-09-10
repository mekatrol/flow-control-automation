namespace Server.Common.Models.FlowExecution;

/// <summary>Represents the canonical state returned for every execution-context operation.</summary>
public sealed record FlowExecutionContext
{
    public required string Id { get; init; }
    public required string FlowId { get; init; }
    public required uint Revision { get; init; }
    public required FlowExecutionMode Mode { get; init; }
    public required FlowExecutionLifecycle Lifecycle { get; init; }
    public required FlowExecutionCapabilities Capabilities { get; init; }
    public IReadOnlyList<FlowDebugBreakpoint> Breakpoints { get; init; } = [];
    public DebugRuntimeSnapshot? Snapshot { get; init; }
    public FlowDebugInspection? Inspection { get; init; }
    public FlowExecutionIo? Io { get; init; }
    public FlowExecutionPresentation Presentation { get; init; } = new();
    public FlowExecutionDiagnostic? Diagnostic { get; init; }
    public uint LeaseRemainingMilliseconds { get; init; }
}