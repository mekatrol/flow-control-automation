namespace Server.Services.Contracts;

public sealed record PointValidationContext(
    IReadOnlyDictionary<string, PointGroup> Groups,
    IReadOnlyDictionary<string, PointSource> Sources)
{
    public static PointValidationContext Empty { get; } = new(
        new Dictionary<string, PointGroup>(StringComparer.Ordinal),
        new Dictionary<string, PointSource>(StringComparer.Ordinal));
}
