namespace Server.Services.Contracts;

public sealed record ControllerDebugWireStatus(
    ulong SessionId,
    byte State,
    uint CoveredBytes,
    uint ArtifactLength,
    uint FlowRevision,
    ulong TickNumber,
    uint LeaseRemainingMilliseconds,
    ushort LastReasonCode,
    string LastReasonPath);
