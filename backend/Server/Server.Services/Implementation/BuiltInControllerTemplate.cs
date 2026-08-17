namespace Server.Services.Implementation;

public static class BuiltInControllerTemplate
{
    public const string Id = "default";

    public static ControllerTemplate Default { get; } = new()
    {
        Id = Id,
        Name = "Flow Control Automation",
        Description = "Built-in unrestricted application target",
        ReadOnly = true,
        Revision = 1,
        Capabilities = new ControllerCapabilities
        {
            PointTypes = [PointValueType.Analog, PointValueType.Digital, PointValueType.MultiState, PointValueType.Integer, PointValueType.Text],
            PointDirections = [DataDirection.Input, DataDirection.Output, DataDirection.InputOutput, DataDirection.Value],
            PointFeatures =
            [
                ControllerPointFeature.Read,
                ControllerPointFeature.Command,
                ControllerPointFeature.Retain,
                ControllerPointFeature.Override,
                ControllerPointFeature.Relinquish,
                ControllerPointFeature.Quality,
                ControllerPointFeature.Alarms,
                ControllerPointFeature.Trends,
            ],
            ConnectorDataTypes = [ConnectorDataType.Any, ConnectorDataType.Boolean, ConnectorDataType.Event, ConnectorDataType.Number, ConnectorDataType.String],
            FlowFunctions = [.. FlowNodeRegistry.Functions],
            ExecutionModes = [ExecutionMode.Event, ExecutionMode.Interval],
            RuntimeFeatures =
            [
                ControllerRuntimeFeature.VirtualPoints,
                ControllerRuntimeFeature.BoundPoints,
                ControllerRuntimeFeature.CommandArbitration,
                ControllerRuntimeFeature.QualityPropagation
            ]
        }
    };
}