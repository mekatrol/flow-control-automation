using Server.Services.Contracts;

namespace Server.Api.Contracts;

public sealed record SetEmulatorInputsRequest
{
    public IReadOnlyList<EmulatorInputChange> Inputs { get; init; } = [];
}