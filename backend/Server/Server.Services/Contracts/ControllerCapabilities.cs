namespace Server.Services.Contracts;

public sealed record ControllerCapabilities
{
    public IReadOnlyList<string> PointTypes { get; init; } = [];
    public IReadOnlyList<string> PointDirections { get; init; } = [];
    public IReadOnlyList<string> PointFeatures { get; init; } = [];
    public IReadOnlyList<string> ConnectorDataTypes { get; init; } = [];
    public IReadOnlyList<string> FlowFunctions { get; init; } = [];
    public IReadOnlyList<string> ExecutionModes { get; init; } = [];
    public IReadOnlyList<string> RuntimeFeatures { get; init; } = [];
}