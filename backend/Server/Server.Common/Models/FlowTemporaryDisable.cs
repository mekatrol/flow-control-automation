namespace Server.Common.Models;

public sealed record FlowTemporaryDisable
{
    public required string ContextId { get; init; }

    public required string StartedAt { get; init; }
}