using Server.Services.Contracts;

namespace Server.Api.Contracts;

public sealed record CreateFlowEmulatorRequest
{
    public required ExecutableFlowSource Source { get; init; }
}