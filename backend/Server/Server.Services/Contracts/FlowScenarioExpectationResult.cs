namespace Server.Services.Contracts;

public sealed record FlowScenarioExpectationResult
{
    public required bool Passed { get; init; }
    public required string OutputId { get; init; }
    public required string Operator { get; init; }
    public ulong? Scan { get; init; }
    public FlowVmValue? ExpectedValue { get; init; }
    public FlowVmValue? ActualValue { get; init; }
    public string? Quality { get; init; }
    public string? DiagnosticCode { get; init; }
}
