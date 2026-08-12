namespace Server.Api.Contracts;

public sealed record ImportFlowIlRequest
{
    public required string ArtifactBase64 { get; init; }
    public string? Name { get; init; }
    public bool Save { get; init; }
}
