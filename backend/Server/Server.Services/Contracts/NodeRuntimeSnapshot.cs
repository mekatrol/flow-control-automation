namespace Server.Services.Contracts;

public sealed record NodeRuntimeSnapshot(string State, string UpdatedAt)
{
    public bool? Value { get; init; }
    public FlowVmValue? TypedValue { get; init; }
}