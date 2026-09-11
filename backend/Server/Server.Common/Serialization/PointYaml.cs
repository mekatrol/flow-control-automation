namespace Server.Common.Serialization;

public static class PointYaml
{
    public static AutomationPoint Parse(ReadOnlySpan<byte> yaml)
    {
        var document = ConfigurationYaml.Parse<PointDocument>(
            yaml,
            ConfigurationKind.Points);

        if (document.Points.Count != 1)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.InvalidShape,
                "YAML must contain exactly one point.");
        }

        return document.Points[0];
    }

    public static string Render(AutomationPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        return ConfigurationYaml.Render(new PointDocument
        {
            Points = [ForTransport(point)]
        });
    }

    private static AutomationPoint ForTransport(AutomationPoint point) => point with
    {
        Revision = 0,
        CreatedAt = null,
        UpdatedAt = null
    };
}
