namespace Server.Services.Contracts;

public static class PointGroupYaml
{
    public static PointGroup Parse(ReadOnlySpan<byte> yaml)
    {
        var document = ConfigurationYaml.Parse<PointDocument>(
            yaml,
            ConfigurationKind.Points);
        if (document.Groups.Count != 1 || document.Points.Count != 0)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.InvalidShape,
                "YAML must contain exactly one group and no points.");
        }

        return document.Groups[0];
    }

    public static string Render(PointGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return ConfigurationYaml.Render(new PointDocument
        {
            Groups =
            [
                group with
                {
                    Revision = 0,
                    CreatedAt = null,
                    UpdatedAt = null,
                },
            ],
        });
    }
}