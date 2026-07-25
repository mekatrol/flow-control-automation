using Server.Services.Contracts;

namespace Server.Api.Contracts;

public sealed record CredentialListResponse(
    IReadOnlyList<CredentialMetadata> Items);