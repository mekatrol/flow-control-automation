using Server.Services.Contracts;

namespace Server.Api.Contracts;

public sealed record CreateDebugSessionRequest
{
    public required ExecutableFlowSource Source { get; init; }
    public bool ReplaceExisting { get; init; }
}
