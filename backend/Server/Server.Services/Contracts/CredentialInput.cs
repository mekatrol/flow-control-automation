using System.Text.Json.Serialization;

namespace Server.Services.Contracts;

public sealed record CredentialInput
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Username { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Token { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Revision { get; init; }
}