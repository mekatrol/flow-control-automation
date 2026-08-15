namespace Server.Services.Contracts;

public sealed record FlowScenarioStep
{
    public required ulong AtMilliseconds { get; init; }
    public required string Action { get; init; }
    public IReadOnlyList<EmulatorInputChange> Inputs { get; init; } = [];
    public bool PowerCycle { get; init; }
}