namespace Server.Api.Contracts;

public sealed record AdvanceEmulatorRequest(ulong Milliseconds, bool Scan = true);
