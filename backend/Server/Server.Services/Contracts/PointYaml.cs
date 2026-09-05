using Server.Common.Models;

namespace Server.Services.Contracts;

public static class PointYaml
{
    public static VirtualAutomationPoint Parse(ReadOnlySpan<byte> yaml)
    {
        var document = ConfigurationYaml.Parse<PointDocument>(
            yaml,
            ConfigurationKind.Points);
        if (document.Points.Count != 1 || document.Groups.Count != 0)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.InvalidShape,
                "YAML must contain exactly one point and no groups.");
        }

        return document.Points[0];
    }

    public static string Render(VirtualAutomationPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return ConfigurationYaml.Render(new PointDocument
        {
            Points = [ForTransport(point)]
        });
    }

    private static VirtualAutomationPoint ForTransport(VirtualAutomationPoint point) => point with
    {
        Revision = 0,
        CreatedAt = null,
        UpdatedAt = null
    };
}