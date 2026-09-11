namespace Server.Common.Models.Points;

public sealed record PointValidationContext(
    IReadOnlyDictionary<string, PointSource> Sources)
{
    public static PointValidationContext Empty { get; } = new(
        new Dictionary<string, PointSource>(StringComparer.Ordinal));
}
