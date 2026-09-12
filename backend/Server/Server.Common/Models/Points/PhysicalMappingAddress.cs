namespace Server.Common.Models.Points;

public sealed record PhysicalMappingAddress
{
    public required string ControllerId { get; init; }
    public required string Channel { get; init; }
    public System.Text.Json.Nodes.JsonNode? Address { get; init; }
    public string? ElectricalType { get; init; }
}