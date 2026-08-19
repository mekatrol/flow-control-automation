namespace Server.Common.Contracts;

public sealed record ValidatedControllerTemplate(
    ControllerTemplate Source,
    IReadOnlySet<FlowPointValueType> PointTypes,
    IReadOnlySet<DataDirection> PointDirections,
    IReadOnlySet<ControllerPointFeature> PointFeatures,
    IReadOnlySet<ConnectorDataType> ConnectorDataTypes,
    IReadOnlySet<FlowFunctionKind> FlowFunctions,
    IReadOnlySet<ExecutionMode> ExecutionModes,
    IReadOnlySet<ControllerRuntimeFeature> RuntimeFeatures);