namespace Server.Services.Contracts;

public sealed record ControllerLimits
{
    public int? MaxFlows { get; init; }
    public int? MaxNodesPerFlow { get; init; }
    public int? MaxConnectionsPerFlow { get; init; }
    public int? MinimumIntervalMilliseconds { get; init; }
}