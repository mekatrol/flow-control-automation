namespace Server.Services.Contracts;

public sealed record FlowScenario
{
    public int SchemaVersion { get; init; } = 1;
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string FlowId { get; init; }
    public required uint FlowRevision { get; init; }
    public IReadOnlyList<FlowScenarioStep> Steps { get; init; } = [];
    public IReadOnlyList<FlowScenarioExpectation> Expectations { get; init; } = [];
}
