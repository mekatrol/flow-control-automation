namespace Server.Services.Contracts;

public sealed record EmulatorOutputSample(
    ulong ScanNumber,
    ulong TimestampMilliseconds,
    string OutputId,
    FlowVmValue ProposedValue,
    FlowVmValue EffectiveValue,
    DataQuality Quality,
    string? Units,
    ulong LastChangeScan,
    bool IsInterface,
    string ArbitrationOwner,
    byte Priority,
    ulong? ExpiresAtMilliseconds);