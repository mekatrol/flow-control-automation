using Server.Common.Types;

namespace Server.Common.Models;

public sealed record ExecutableFlowExecution
{
    public FlowExecutionModeType Mode { get; init; } = FlowExecutionModeType.Manual;
    public uint IntervalMs { get; init; }
    public InputQualityPolicyType InputQualityPolicy { get; init; } = InputQualityPolicyType.RequireGood;
}