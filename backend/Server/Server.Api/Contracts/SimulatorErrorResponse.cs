namespace Server.Api.Contracts;

/// <summary>Describes a stable simulator or emulator failure and its optional source location.</summary>
/// <param name="Code">The non-empty stable error vocabulary value used for programmatic handling.</param>
/// <param name="Message">A non-empty human-readable explanation; callers must not parse it as a stable identifier.</param>
/// <param name="Path">The optional JSON Pointer-like path to the invalid contract value; <see langword="null"/> means the failure is not field-specific.</param>
/// <param name="NodeId">The optional flow node identifier associated with the failure; <see langword="null"/> means no single node is responsible.</param>
/// <param name="Details">Optional structured, non-secret diagnostic data whose shape is selected by <paramref name="Code"/>.</param>
public sealed record SimulatorErrorResponse(
    string Code,
    string Message,
    string? Path = null,
    string? NodeId = null,
    object? Details = null);