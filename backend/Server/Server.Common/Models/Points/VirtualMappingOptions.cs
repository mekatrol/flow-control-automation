namespace Server.Common.Models.Points;

public sealed record VirtualMappingOptions
{
    public string Persistence { get; init; } = "volatile";
}