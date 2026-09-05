using Server.Common.Models;
using Server.Common.Services;
using Server.Common.Types;

namespace Server.Common;

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
            PointTypes = [AutomationPointValueType.Analog, AutomationPointValueType.Digital, AutomationPointValueType.MultiState, AutomationPointValueType.Integer, AutomationPointValueType.Text],
            PointDirections = [DataDirectionType.Input, DataDirectionType.Output, DataDirectionType.InputOutput, DataDirectionType.Value],
            PointFeatures =
            [
                ControllerPointFeatureType.Read,
                ControllerPointFeatureType.Command,
                ControllerPointFeatureType.Retain,
                ControllerPointFeatureType.Override,
                ControllerPointFeatureType.Relinquish,
                ControllerPointFeatureType.Quality,
                ControllerPointFeatureType.Alarms,
                ControllerPointFeatureType.Trends,
            ],
            ConnectorDataTypes = [ConnectorDataType.Any, ConnectorDataType.Boolean, ConnectorDataType.Event, ConnectorDataType.Number, ConnectorDataType.String],
            FlowFunctions = [.. FlowNodeRegistry.Functions],
            ExecutionModes = [ExecutionModeType.Event, ExecutionModeType.Interval],
            RuntimeFeatures =
            [
                ControllerRuntimeFeatureType.VirtualPoints,
                ControllerRuntimeFeatureType.PhysicalPoints,
                ControllerRuntimeFeatureType.CommandArbitration,
                ControllerRuntimeFeatureType.QualityPropagation
            ]
        }
    };
}