namespace Server.Api.Contracts;

/// <summary>Supplies the user-visible identity for a newly created flow.</summary>
/// <param name="Name">The non-empty display name after trimming; it must satisfy the flow validation limits and need not be globally unique.</param>
public sealed record CreateFlowRequest(string Name);