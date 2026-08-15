namespace Server.Services.Contracts;

public sealed record FlowScenarioRunResult
{
    public required string ScenarioId { get; init; }
    public required bool Passed { get; init; }
    public required ulong ScanNumber { get; init; }
    public IReadOnlyList<FlowScenarioExpectationResult> Expectations { get; init; } = [];
}