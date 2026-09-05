using Server.Common.Types;

namespace Server.Common.Models;

public sealed record ValidatedControllerTemplate(
    ControllerTemplate Source,
    IReadOnlySet<AutomationPointValueType> PointTypes,
    IReadOnlySet<DataDirectionType> PointDirections,
    IReadOnlySet<ControllerPointFeatureType> PointFeatures,
    IReadOnlySet<ConnectorDataType> ConnectorDataTypes,
    IReadOnlySet<FlowFunctionType> FlowFunctions,
    IReadOnlySet<ExecutionModeType> ExecutionModes,
    IReadOnlySet<ControllerRuntimeFeatureType> RuntimeFeatures);