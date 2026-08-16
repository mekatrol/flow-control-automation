using Server.Services.Extensions;

namespace Server.Services.Contracts;

public sealed record FlowVmInput
{
    public FlowVmInput(string pointId, bool value, bool isGood = true, bool isInterface = false)
        : this(pointId, FlowVmValue.FromBoolean(value, isGood ? DataQualityExtensions.Good : DataQualityExtensions.Bad), isInterface) { }

    public FlowVmInput(string pointId, FlowVmValue typedValue, bool isInterface = false)
    {
        PointId = pointId;
        TypedValue = typedValue;
        IsInterface = isInterface;
    }

    public string PointId { get; }
    public FlowVmValue TypedValue { get; }
    public bool Value => TypedValue.Boolean;
    public bool IsGood => TypedValue.Quality == DataQualityExtensions.Good;
    public bool IsInterface { get; }
}