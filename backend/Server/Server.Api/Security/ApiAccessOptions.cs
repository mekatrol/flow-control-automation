namespace Server.Api.Security;

public sealed class ApiAccessOptions
{
    public const string SectionName = "ApiAccess";
    public Dictionary<string, ApiIdentityOptions> Identities { get; init; } = new(StringComparer.Ordinal);
}