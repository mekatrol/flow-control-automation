namespace Server.Services.Contracts;

public sealed record ValidatedControllerTemplate(
    ControllerTemplate Source,
    IReadOnlySet<PointValueType> PointTypes,
    IReadOnlySet<PointDirection> PointDirections,
    IReadOnlySet<ControllerPointFeature> PointFeatures,
    IReadOnlySet<ConnectorDataType> ConnectorDataTypes,
    IReadOnlySet<string> FlowFunctions,
    IReadOnlySet<ExecutionMode> ExecutionModes,
    IReadOnlySet<ControllerRuntimeFeature> RuntimeFeatures);