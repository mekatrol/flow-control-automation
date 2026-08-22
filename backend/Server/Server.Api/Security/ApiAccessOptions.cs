namespace Server.Api.Security;

public sealed class ApiAccessOptions
{
    public const string SectionName = "ApiAccess";
    public string? FrontendIdentity { get; init; }
    public Dictionary<string, ApiIdentityOptions> Identities { get; init; } = new(StringComparer.Ordinal);
}