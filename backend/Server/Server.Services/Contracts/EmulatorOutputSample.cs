using Server.Common.Types;

namespace Server.Services.Contracts;

public sealed record EmulatorOutputSample(
    ulong ScanNumber,
    ulong TimestampMilliseconds,
    string OutputId,
    FlowVmValue ProposedValue,
    FlowVmValue EffectiveValue,
    DataQualityType Quality,
    string? Units,
    ulong LastChangeScan,
    string ArbitrationOwner,
    byte Priority,
    ulong? ExpiresAtMilliseconds);