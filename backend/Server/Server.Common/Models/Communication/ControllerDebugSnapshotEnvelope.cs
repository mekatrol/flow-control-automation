namespace Server.Common.Models.Communication;

public sealed record ControllerDebugSnapshotEnvelope(
    ulong SessionId,
    ulong TickNumber,
    ReadOnlyMemory<byte> Bytes,
    ReadOnlyMemory<byte> Sha256);