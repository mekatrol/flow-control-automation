namespace Server.Services.Contracts;

public sealed record FlowVmCommand
{
    public FlowVmCommand(string pointId, bool value) : this(pointId, FlowVmValue.FromBoolean(value)) { }
    public FlowVmCommand(string pointId, FlowVmValue typedValue) { PointId = pointId; TypedValue = typedValue; }
    public string PointId { get; }
    public FlowVmValue TypedValue { get; }
    public bool Value => TypedValue.Boolean;
}
