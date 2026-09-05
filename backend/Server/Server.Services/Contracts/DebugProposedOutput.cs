using Server.Common.Types;

namespace Server.Services.Contracts;

public sealed record DebugProposedOutput(
    string PointId,
    string State,
    DataQualityType Quality,
    bool ProposedValue,
    double? ProposedNumber = null,
    FlowVmValue? TypedValue = null);