namespace Server.Common.Models.Points;

/// <summary>A source-local communication operation and the aliases it exposes.</summary>
public record PointMapping
{
    public string Id { get; init; } = string.Empty;
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public MappingReadOperation? Read { get; init; }
    public MappingCommandOperation? Command { get; init; }
    public PhysicalMappingAddress? Physical { get; init; }
    public VirtualMappingOptions? Virtual { get; init; }
}