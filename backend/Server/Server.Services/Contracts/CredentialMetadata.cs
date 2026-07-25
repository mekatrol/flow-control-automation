namespace Server.Services.Contracts;

public sealed record CredentialMetadata(
    string Id,
    string Name,
    string Kind,
    string? Username,
    int Revision,
    string CreatedAt,
    string UpdatedAt);