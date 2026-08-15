namespace Server.Services.Contracts;

public sealed record FlowVmCommand
{
    public FlowVmCommand(string pointId, bool value, bool isInterface = false) : this(pointId, FlowVmValue.FromBoolean(value), isInterface) { }
    public FlowVmCommand(string pointId, FlowVmValue typedValue, bool isInterface = false) { PointId = pointId; TypedValue = typedValue; IsInterface = isInterface; }
    public string PointId { get; }
    public FlowVmValue TypedValue { get; }
    public bool Value => TypedValue.Boolean;
    public bool IsInterface { get; }
}