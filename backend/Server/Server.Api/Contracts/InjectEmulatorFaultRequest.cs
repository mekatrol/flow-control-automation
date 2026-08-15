namespace Server.Api.Contracts;

/// <summary>Changes the synthetic fault exposed by an emulator session.</summary>
/// <param name="Fault">The supported fault identifier understood by the emulator, or <see langword="null"/> to clear the active synthetic fault.</param>
public sealed record InjectEmulatorFaultRequest(string? Fault);