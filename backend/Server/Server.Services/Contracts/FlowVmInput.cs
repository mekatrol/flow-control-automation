namespace Server.Services.Contracts;

public sealed record FlowVmInput
{
    public FlowVmInput(string pointId, bool value, bool isGood = true)
        : this(pointId, FlowVmValue.FromBoolean(value, isGood ? "good" : "bad")) { }

    public FlowVmInput(string pointId, FlowVmValue typedValue)
    {
        PointId = pointId;
        TypedValue = typedValue;
    }

    public string PointId { get; }
    public FlowVmValue TypedValue { get; }
    public bool Value => TypedValue.Boolean;
    public bool IsGood => TypedValue.Quality == "good";
}
