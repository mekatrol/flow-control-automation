namespace Server.Common.Models;

public sealed record ControllerTemplate
{
    public int SchemaVersion { get; init; } = 1;
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool ReadOnly { get; init; }
    public ControllerCapabilities Capabilities { get; init; } = new();
    public ControllerLimits Limits { get; init; } = new();
    public int Revision { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}