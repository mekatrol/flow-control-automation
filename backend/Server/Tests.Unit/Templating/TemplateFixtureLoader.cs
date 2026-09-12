using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tests.Unit.Templating;

internal static class TemplateFixtureLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    public static IReadOnlyList<TemplateFixture> LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var fixtures = Directory
            .EnumerateFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(Load)
            .ToArray();
        var duplicate = fixtures
            .GroupBy(fixture => fixture.Name!, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new TemplateFixtureException(
                $"Duplicate template fixture name '{duplicate.Key}'.");
        }

        return fixtures;
    }

    public static TemplateFixture Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var yaml = File.ReadAllText(path);

        try
        {
            RejectDuplicateKeysAndMultipleDocuments(yaml);
            var fixture = Deserializer.Deserialize<TemplateFixture>(yaml)
                ?? throw new TemplateFixtureException("Template fixture is empty.");

            Validate(fixture);

            return fixture;
        }
        catch (TemplateFixtureException)
        {
            throw;
        }
        catch (Exception exception) when (exception is YamlException or InvalidOperationException)
        {
            throw new TemplateFixtureException("Invalid template fixture YAML.", exception);
        }
    }

    private static void RejectDuplicateKeysAndMultipleDocuments(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count != 1)
        {
            throw new TemplateFixtureException("A template fixture must contain one YAML document.");
        }

        RejectDuplicateKeys(stream.Documents[0].RootNode);
    }

    private static void RejectDuplicateKeys(YamlNode node)
    {
        if (node is YamlMappingNode mapping)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in mapping.Children)
            {
                if (entry.Key is not YamlScalarNode { Value: not null } key)
                {
                    throw new TemplateFixtureException("Fixture mapping keys must be strings.");
                }

                if (!keys.Add(key.Value))
                {
                    throw new TemplateFixtureException($"Duplicate YAML key '{key.Value}'.");
                }

                RejectDuplicateKeys(entry.Value);
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (var child in sequence.Children)
            {
                RejectDuplicateKeys(child);
            }
        }
    }

    private static void Validate(TemplateFixture fixture)
    {
        if (string.IsNullOrWhiteSpace(fixture.Name))
        {
            throw new TemplateFixtureException("Template fixture must declare a name.");
        }

        if (fixture.Template is null)
        {
            throw new TemplateFixtureException(
                $"Template fixture '{fixture.Name}' must declare a template.");
        }

        if ((fixture.Expected is null) == (fixture.ExpectedError is null))
        {
            throw new TemplateFixtureException(
                $"Template fixture '{fixture.Name}' must declare exactly one of expected and expectedError.");
        }
    }
}