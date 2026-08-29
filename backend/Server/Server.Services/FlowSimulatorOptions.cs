namespace Server.Services;

public sealed class FlowSimulatorOptions
{
    public const string SectionName = "FlowSimulator";

    public double SessionLeaseSeconds { get; set; } = 3;
}