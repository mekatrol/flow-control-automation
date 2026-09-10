namespace Server.Common.Models.FlowExecution;

/// <summary>Requests creation and automatic preparation of an execution context for a saved flow.</summary>
public sealed record CreateFlowExecutionContext
{
    public required string FlowId { get; init; }
    public required FlowExecutionMode Mode { get; init; }
    public required uint ExpectedRevision { get; init; }
    public required string TargetId { get; init; }
    public bool ReplaceExisting { get; init; }
    public IReadOnlyList<FlowDebugBreakpoint> Breakpoints { get; init; } = [];
}