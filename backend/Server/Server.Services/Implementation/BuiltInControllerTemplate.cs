using Server.Services.Contracts;

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
        Capabilities = new ControllerCapabilities
        {
            PointTypes = ["analog", "digital", "multi_state", "integer", "text"],
            PointDirections = ["input", "output", "input_output", "value"],
            PointFeatures =
            [
                "read", "command", "retain", "override", "relinquish",
                "quality", "alarms", "trends",
            ],
            ConnectorDataTypes = ["any", "boolean", "event", "number", "string"],
            FlowFunctions = FlowNodeRegistry.Functions.ToArray(),
            ExecutionModes = ["event", "interval"],
            RuntimeFeatures =
            [
                "virtual_points", "bound_points", "command_arbitration",
                "quality_propagation",
            ],
        },
    };
}