namespace Server.Api.Contracts;

/// <summary>Records explicit confirmation of the physical points a debug session may command.</summary>
public sealed record EnableLiveOutputRequest
{
    /// <summary>Gets the distinct, non-empty output point identifiers confirmed by the operator; every ID must belong to the debugged flow and be commandable.</summary>
    public required IReadOnlyList<string> ConfirmedPointIds { get; init; }
}