using System.Text.Json.Serialization;

namespace Server.Common.Models.Points;

public sealed record PointSource
{
    public int SchemaVersion { get; init; } = 1;
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Enabled { get; init; }
    public required PointSourceKind Kind { get; init; }
    public PointSourceConnection Connection { get; init; } = new();
    public string? CredentialRef { get; init; }
    public TlsOptions Tls { get; init; } = new();
    public PointSourceTimeouts Timeouts { get; init; } = new();
    public IReadOnlyList<PointMapping> Mappings { get; init; } = [];
    public IReadOnlyList<AutomationPoint> Points { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Revision { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CreatedAt { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UpdatedAt { get; init; }
}