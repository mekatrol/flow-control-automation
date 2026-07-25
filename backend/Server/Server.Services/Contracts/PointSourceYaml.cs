namespace Server.Services.Contracts;

public static class PointSourceYaml
{
    public static PointSource Parse(ReadOnlySpan<byte> yaml)
    {
        var document = ConfigurationYaml.Parse<PointSourceDocument>(
            yaml,
            ConfigurationKind.PointSources);
        if (document.Sources.Count != 1)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.InvalidShape,
                "YAML must contain exactly one source.");
        }

        return document.Sources[0];
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
        return ConfigurationYaml.Render(
            new PointSourceDocument { Sources = [transportSource] });
    }
}