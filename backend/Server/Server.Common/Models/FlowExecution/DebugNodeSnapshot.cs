namespace Server.Common.Models.FlowExecution;

public sealed record DebugNodeSnapshot(
    string NodeId,
    string State,
    DataQualityType Quality,
    DebugTypedValue? TypedValue);