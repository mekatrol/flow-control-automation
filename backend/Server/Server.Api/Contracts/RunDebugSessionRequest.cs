namespace Server.Api.Contracts;

/// <summary>Requests continuous execution of a paused debug session.</summary>
/// <param name="IntervalMilliseconds">The scan interval in milliseconds; it must be positive and within the runtime's supported scheduling bounds.</param>
public sealed record RunDebugSessionRequest(uint IntervalMilliseconds);