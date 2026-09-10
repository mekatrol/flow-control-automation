namespace Server.Common.Models.FlowExecution;

public sealed record EmulatorInputChange(
    string InputId,
    FlowVmValue TypedValue,
    ulong? EffectiveAtMilliseconds = null);