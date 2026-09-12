namespace Server.Common.Serialization;

public static class PointSourceYaml
{
    public static PointSource Parse(ReadOnlySpan<byte> yaml)
    {
        return ConfigurationYaml.Parse<PointSource>(yaml, ConfigurationKind.PointSources);
    }

    public static string Render(PointSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var transportSource = source with
        {
            Revision = 0,
            CreatedAt = null,
            UpdatedAt = null
        };

        return ConfigurationYaml.Render(transportSource);
    }
}