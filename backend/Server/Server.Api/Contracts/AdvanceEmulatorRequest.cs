namespace Server.Api.Contracts;

/// <summary>Requests deterministic advancement of an emulator's monotonic clock.</summary>
/// <param name="Milliseconds">The non-negative number of milliseconds to advance; zero keeps the current time and is valid when requesting a scan only.</param>
/// <param name="Scan">Whether to execute one PLC scan after advancing time; <see langword="false"/> changes time without evaluating the flow.</param>
public sealed record AdvanceEmulatorRequest(ulong Milliseconds, bool Scan = true);