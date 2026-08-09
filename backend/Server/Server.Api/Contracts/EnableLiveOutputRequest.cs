namespace Server.Api.Contracts;

public sealed record EnableLiveOutputRequest
{
    public required IReadOnlyList<string> ConfirmedPointIds { get; init; }
}
