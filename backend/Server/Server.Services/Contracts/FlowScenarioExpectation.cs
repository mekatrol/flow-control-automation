namespace Server.Services.Contracts;

public sealed record FlowScenarioExpectation
{
    public ulong? Scan { get; init; }
    public required string OutputId { get; init; }
    public required string Operator { get; init; }
    public FlowVmValue? ExpectedValue { get; init; }
    public double? Tolerance { get; init; }
}
