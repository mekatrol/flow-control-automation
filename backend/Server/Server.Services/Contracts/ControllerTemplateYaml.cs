using Server.Common.Models;
using System.Text.Json;

namespace Server.Services.Contracts;

public static class ControllerTemplateYaml
{
    public static ControllerTemplate Parse(ReadOnlySpan<byte> yaml)
    {
        try
        {
            return ConfigurationYaml.Parse<ControllerTemplate>(
                yaml,
                ConfigurationKind.Controller);
        }
        catch (JsonException exception)
        {
            throw new ConfigurationYamlException(
                ConfigurationYamlError.InvalidShape,
                "YAML does not match the controller template contract.",
                exception);
        }
    }

    public static string Render(ControllerTemplate template) =>
        ConfigurationYaml.Render(template);
}