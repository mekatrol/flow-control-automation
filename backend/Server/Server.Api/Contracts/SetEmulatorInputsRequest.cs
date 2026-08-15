using Server.Services.Contracts;

namespace Server.Api.Contracts;

/// <summary>Defines input changes to stage for an emulator at deterministic scan boundaries.</summary>
public sealed record SetEmulatorInputsRequest
{
    /// <summary>Gets input changes in submission order; the list may be empty, point identifiers must be unique per effective time, and each value must match its point type.</summary>
    public IReadOnlyList<EmulatorInputChange> Inputs { get; init; } = [];
}