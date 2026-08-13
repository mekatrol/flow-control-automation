namespace Server.Services.Contracts;

public sealed record EmulatorInputChange(
    string PointId,
    bool Value,
    bool IsGood = true,
    ulong? EffectiveAtMilliseconds = null);
