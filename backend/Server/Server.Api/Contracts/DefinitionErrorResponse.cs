namespace Server.Api.Contracts;

/// <summary>Describes a stable, machine-readable definition validation failure.</summary>
/// <param name="Message">A non-empty human-readable diagnostic suitable for display; callers must not parse it as a stable identifier.</param>
/// <param name="Code">The non-empty stable error vocabulary value used by clients for programmatic handling.</param>
/// <param name="Details">Optional structured, non-secret context whose shape is determined by <paramref name="Code"/>; <see langword="null"/> means no additional context is available.</param>
public sealed record DefinitionErrorResponse(
    string Message,
    string Code,
    object? Details = null);