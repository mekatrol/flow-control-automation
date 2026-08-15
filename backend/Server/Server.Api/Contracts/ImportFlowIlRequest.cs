namespace Server.Api.Contracts;

/// <summary>Supplies a portable Flow IL artifact for validation and decompilation.</summary>
public sealed record ImportFlowIlRequest
{
    /// <summary>Gets the non-empty RFC 4648 Base64 representation of one bounded Flow IL artifact; decoded size and version must satisfy the current import limits.</summary>
    public required string ArtifactBase64 { get; init; }

    /// <summary>Gets an optional replacement display name for the recovered flow; <see langword="null"/> preserves the artifact-derived name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets whether the recovered flow is persisted after successful validation; <see langword="false"/> performs a preview-only import.</summary>
    public bool Save { get; init; }
}