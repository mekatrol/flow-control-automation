using Server.Common.Types;

namespace Server.Common.Models;

public sealed record ControllerCapabilities
{
    public IReadOnlyList<AutomationPointValueType> PointTypes { get; init; } = [];
    public IReadOnlyList<DataDirectionType> PointDirections { get; init; } = [];
    public IReadOnlyList<ControllerPointFeatureType> PointFeatures { get; init; } = [];
    public IReadOnlyList<ConnectorDataType> ConnectorDataTypes { get; init; } = [];
    public IReadOnlyList<FlowFunctionType> FlowFunctions { get; init; } = [];
    public IReadOnlyList<ExecutionModeType> ExecutionModes { get; init; } = [];
    public IReadOnlyList<ControllerRuntimeFeatureType> RuntimeFeatures { get; init; } = [];
}