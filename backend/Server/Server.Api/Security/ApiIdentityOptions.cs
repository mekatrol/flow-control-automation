namespace Server.Api.Security;

public sealed class ApiIdentityOptions
{
    public required string Key { get; init; }
    public string[] Permissions { get; init; } = [];
}