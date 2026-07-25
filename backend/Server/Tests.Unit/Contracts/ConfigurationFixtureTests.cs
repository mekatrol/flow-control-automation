using Server.Services.Contracts;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tests.Unit.Contracts;

public sealed class ConfigurationFixtureTests
{
    private static string FixtureRoot =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "ContractFixtures");

    private static IEnumerable<TestCaseData> ValidFixtures()
    {
        yield return new TestCaseData(
            "points/v1.yaml",
            "points/v1.normalized.json",
            ConfigurationKind.Points,
            false);
        yield return new TestCaseData(
            "point-sources/v1.yaml",
            "point-sources/v1.normalized.json",
            ConfigurationKind.PointSources,
            false);
        yield return new TestCaseData(
            "controllers/default.v1.yaml",
            "controllers/default.v1.normalized.json",
            ConfigurationKind.Controller,
            true);
        yield return new TestCaseData(
            "controllers/constrained.v1.yaml",
            "controllers/constrained.v1.normalized.json",
            ConfigurationKind.Controller,
            true);
    }

    private static IEnumerable<TestCaseData> InvalidFixtures()
    {
        yield return Invalid(
            "points/invalid/unknown-field.yaml",
            ConfigurationKind.Points,
            ConfigurationYamlError.UnknownField);
        yield return Invalid(
            "point-sources/invalid/unknown-field.yaml",
            ConfigurationKind.PointSources,
            ConfigurationYamlError.UnknownField);
        yield return Invalid(
            "controllers/invalid/unsupported-schema.yaml",
            ConfigurationKind.Controller,
            ConfigurationYamlError.UnsupportedSchema);
        yield return Invalid(
            "controllers/invalid/alias.yaml",
            ConfigurationKind.Controller,
            ConfigurationYamlError.UnsupportedFeature);
        yield return Invalid(
            "controllers/invalid/syntax.yaml",
            ConfigurationKind.Controller,
            ConfigurationYamlError.Syntax);
        yield return Invalid(
            "controllers/invalid/unknown-field.yaml",
            ConfigurationKind.Controller,
            ConfigurationYamlError.UnknownField);
    }

    [TestCaseSource(nameof(ValidFixtures))]
    public void Parse_MatchesNormalizedJsonAfterMetadataRemoval(
        string yamlFile,
        string jsonFile,
        ConfigurationKind kind,
        bool wrapController)
    {
        var yaml = File.ReadAllBytes(Path.Combine(FixtureRoot, yamlFile));
        JsonNode actual = ConfigurationYaml.Parse(yaml, kind);
        if (wrapController)
        {
            var controller = actual.AsObject();
            var schemaVersion = controller["schemaVersion"]?.DeepClone();
            controller.Remove("schemaVersion");
            actual = new JsonObject
            {
                ["schemaVersion"] = schemaVersion,
                ["templates"] = new JsonArray(controller),
            };
        }

        var expected = JsonNode.Parse(
            File.ReadAllText(Path.Combine(FixtureRoot, jsonFile)))
            ?? throw new InvalidOperationException("JSON fixture is empty.");
        StripBackendMetadata(expected);

        Assert.That(
            JsonNode.DeepEquals(actual, expected),
            Is.True,
            $"YAML:{Environment.NewLine}{actual}{Environment.NewLine}JSON:{Environment.NewLine}{expected}");
    }

    [TestCaseSource(nameof(InvalidFixtures))]
    public void Parse_RejectsInvalidFixturesForExpectedReason(
        string yamlFile,
        ConfigurationKind kind,
        ConfigurationYamlError expectedError)
    {
        var yaml = File.ReadAllBytes(Path.Combine(FixtureRoot, yamlFile));

        var exception = Assert.Throws<ConfigurationYamlException>(
            () => ConfigurationYaml.Parse(yaml, kind));

        Assert.That(exception!.Category, Is.EqualTo(expectedError));
    }

    [Test]
    public void Parse_RejectsDuplicateKeysCustomTagsMultipleDocumentsAndExcessiveDepth()
    {
        var cases = new[]
        {
            ("schemaVersion: 1\nsources: []\nsources: []\n", ConfigurationYamlError.UnsupportedFeature),
            ("schemaVersion: 1\nsources: !custom []\n", ConfigurationYamlError.UnsupportedFeature),
            ("schemaVersion: 1\nsources: []\n---\nschemaVersion: 1\nsources: []\n", ConfigurationYamlError.MultipleDocuments),
            (
                $"schemaVersion: 1\nsources:\n  - id: source\n    name: source\n    enabled: true\n    kind: http_json\n    connection:\n{new string(' ', 8)}nested: [{string.Concat(Enumerable.Repeat("[", 21))}0{string.Concat(Enumerable.Repeat("]", 21))}]\n    tls: {{verifyServerCertificate: true}}\n    timeouts: {{connectMilliseconds: 100}}\n",
                ConfigurationYamlError.ExcessiveNesting),
        };

        Assert.Multiple(() =>
        {
            foreach (var (yaml, expected) in cases)
            {
                var exception = Assert.Throws<ConfigurationYamlException>(
                    () => ConfigurationYaml.Parse(
                        Encoding.UTF8.GetBytes(yaml),
                        ConfigurationKind.PointSources));
                Assert.That(exception!.Category, Is.EqualTo(expected));
            }
        });
    }

    [Test]
    public void Parse_RejectsOversizedInputBeforeParsing()
    {
        var yaml = new byte[ConfigurationYaml.MaximumBytes + 1];

        var exception = Assert.Throws<ConfigurationYamlException>(
            () => ConfigurationYaml.Parse(yaml, ConfigurationKind.Points));

        Assert.That(exception!.Category, Is.EqualTo(ConfigurationYamlError.TooLarge));
    }

    [Test]
    public void TypedParseAndRender_PreservePointSourceContract()
    {
        var yaml = File.ReadAllBytes(Path.Combine(FixtureRoot, "point-sources/v1.yaml"));
        var document = ConfigurationYaml.Parse<PointSourceDocument>(
            yaml,
            ConfigurationKind.PointSources);

        var renderedText = ConfigurationYaml.Render(document);
        Assert.That(renderedText, Does.StartWith("schemaVersion: 1\nsources:\n"));
        Assert.That(renderedText, Does.Contain("\n- id:"));
        Assert.That(renderedText.TrimStart(), Does.Not.StartWith("{"));

        var rendered = Encoding.UTF8.GetBytes(renderedText);
        var reparsed = ConfigurationYaml.Parse<PointSourceDocument>(
            rendered,
            ConfigurationKind.PointSources);

        var originalJson = JsonSerializer.SerializeToNode(document, FlowControlJson.Options);
        var reparsedJson = JsonSerializer.SerializeToNode(reparsed, FlowControlJson.Options);
        Assert.That(JsonNode.DeepEquals(reparsedJson, originalJson), Is.True);
    }

    private static TestCaseData Invalid(
        string file,
        ConfigurationKind kind,
        ConfigurationYamlError error)
    {
        return new TestCaseData(file, kind, error);
    }

    private static void StripBackendMetadata(JsonNode node)
    {
        if (node is JsonObject value)
        {
            value.Remove("revision");
            value.Remove("createdAt");
            value.Remove("updatedAt");
            foreach (var child in value.Select(item => item.Value).OfType<JsonNode>())
            {
                StripBackendMetadata(child);
            }
        }
        else if (node is JsonArray items)
        {
            foreach (var child in items.OfType<JsonNode>())
            {
                StripBackendMetadata(child);
            }
        }
    }
}