namespace Server.Services.Contracts;

public sealed record ControllerDebugLoadResult(ulong SessionId, ushort ChunkLimit, uint LeaseMilliseconds);
