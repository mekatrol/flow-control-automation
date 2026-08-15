namespace Server.Api.Contracts;

/// <summary>Requests restoration of an emulator to its initial deterministic state.</summary>
/// <param name="PowerCycle">Whether persistent runtime state is reset as if power were removed; <see langword="false"/> performs a warm reset that preserves persistent values.</param>
public sealed record ResetEmulatorRequest(bool PowerCycle = false);