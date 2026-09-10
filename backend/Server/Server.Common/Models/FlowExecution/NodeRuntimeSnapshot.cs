namespace Server.Common.Models.FlowExecution;

public sealed record NodeRuntimeSnapshot(string State, string UpdatedAt)
{
    public bool? Value { get; init; }
    public FlowVmValue? TypedValue { get; init; }
}