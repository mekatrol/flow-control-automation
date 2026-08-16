namespace Server.Services.Contracts;

public sealed record DebugProposedOutput(
    string PointId,
    string State,
    DataQuality Quality,
    bool ProposedValue,
    double? ProposedNumber = null,
    FlowVmValue? TypedValue = null);