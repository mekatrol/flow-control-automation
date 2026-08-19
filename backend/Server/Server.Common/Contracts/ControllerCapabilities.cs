namespace Server.Common.Contracts;

public sealed record ControllerCapabilities
{
    public IReadOnlyList<FlowPointValueType> PointTypes { get; init; } = [];
    public IReadOnlyList<DataDirection> PointDirections { get; init; } = [];
    public IReadOnlyList<ControllerPointFeature> PointFeatures { get; init; } = [];
    public IReadOnlyList<ConnectorDataType> ConnectorDataTypes { get; init; } = [];
    public IReadOnlyList<FlowFunctionKind> FlowFunctions { get; init; } = [];
    public IReadOnlyList<ExecutionMode> ExecutionModes { get; init; } = [];
    public IReadOnlyList<ControllerRuntimeFeature> RuntimeFeatures { get; init; } = [];
}