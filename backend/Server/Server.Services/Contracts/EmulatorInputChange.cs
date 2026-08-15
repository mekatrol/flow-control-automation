namespace Server.Services.Contracts;

public sealed record EmulatorInputChange(
    string InputId,
    FlowVmValue TypedValue,
    ulong? EffectiveAtMilliseconds = null);