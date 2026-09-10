namespace Server.Common.Models.Communication;

public sealed record ControllerDebugLoadResult(ulong SessionId, ushort ChunkLimit, uint LeaseMilliseconds);