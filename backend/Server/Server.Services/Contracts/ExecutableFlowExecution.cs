namespace Server.Services.Contracts;

public sealed record ExecutableFlowExecution
{
    public FlowExecutionMode Mode { get; init; } = FlowExecutionMode.Manual;
    public uint IntervalMs { get; init; }
    public InputQualityPolicy InputQualityPolicy { get; init; } = InputQualityPolicy.RequireGood;
}