namespace Server.Services.Contracts;

public sealed record EmulatorOutputSample(
    ulong ScanNumber,
    ulong TimestampMilliseconds,
    string PointId,
    bool ProposedValue,
    bool EffectiveValue,
    string Quality,
    string ArbitrationOwner,
    byte Priority,
    ulong? ExpiresAtMilliseconds);
