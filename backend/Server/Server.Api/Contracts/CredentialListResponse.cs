using Server.Services.Contracts;

namespace Server.Api.Contracts;

/// <summary>Returns credential metadata without exposing stored secret values.</summary>
/// <param name="Items">The credential metadata in the service's deterministic listing order; the collection may be empty and never contains secret material.</param>
public sealed record CredentialListResponse(
    IReadOnlyList<CredentialMetadata> Items);