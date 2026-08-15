namespace Server.Services.Contracts;

public sealed record ExecutableFlowExecution
{
    public string Mode { get; init; } = "manual";
    public uint IntervalMs { get; init; }
    public string InputQualityPolicy { get; init; } = "require_good";
}