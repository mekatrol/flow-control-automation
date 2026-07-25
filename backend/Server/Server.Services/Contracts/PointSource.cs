using System.Text.Json.Serialization;

namespace Server.Services.Contracts;

public sealed record PointSource
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Enabled { get; init; }
    public required string Kind { get; init; }
    public PointSourceConnection Connection { get; init; } = new();
    public string? CredentialRef { get; init; }
    public TlsOptions Tls { get; init; } = new();
    public PointSourceTimeouts Timeouts { get; init; } = new();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Revision { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CreatedAt { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UpdatedAt { get; init; }
}