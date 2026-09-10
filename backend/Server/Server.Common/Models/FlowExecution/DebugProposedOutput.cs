namespace Server.Common.Models.FlowExecution;

public sealed record DebugProposedOutput(
    string PointId,
    string State,
    DataQualityType Quality,
    bool ProposedValue,
    double? ProposedNumber = null,
    FlowVmValue? TypedValue = null);