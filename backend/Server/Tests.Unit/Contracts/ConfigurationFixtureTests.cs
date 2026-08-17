using Server.Services.Contracts;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tests.Unit.Helpers;

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
        JsonNode actual = ConfigurationYaml.ParseToJson(yaml, kind);

        if (wrapController)
        {
            var controller = actual.AsObject();
            var schemaVersion = controller["schemaVersion"]?.DeepClone();
            controller.Remove("schemaVersion");
            actual = new JsonObject
            {
                ["schemaVersion"] = schemaVersion,
                ["templates"] = new JsonArray(controller)
            };
        }

        var expected = JsonNode.Parse(
            File.ReadAllText(Path.Combine(FixtureRoot, jsonFile)))
            ?? throw new InvalidOperationException("JSON fixture is empty.");

        StripBackendMetadata(expected);

        // Expected outcome: `JsonNode.DeepEquals(actual` confirms the required condition.
        // Acceptance criteria: `JsonNode.DeepEquals(actual` must be true, because this condition proves that
        // parse matches normalized json after metadata removal.
        var differences = JsonDiff.FindDifferences(actual, expected).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                    differences,
                    Is.Empty,
                    $"JSON differs:{Environment.NewLine}{string.Join(Environment.NewLine, differences)}");

            Assert.That(
                JsonNode.DeepEquals(actual, expected),
                Is.True,
                $"YAML:{Environment.NewLine}{actual}{Environment.NewLine}JSON:{Environment.NewLine}{expected}");
        }
    }

    [TestCaseSource(nameof(InvalidFixtures))]
    public void Parse_RejectsInvalidFixturesForExpectedReason(
        string yamlFile,
        ConfigurationKind kind,
        ConfigurationYamlError expectedError)
    {
        var yaml = File.ReadAllBytes(Path.Combine(FixtureRoot, yamlFile));

        // Expected outcome: The invalid operation is rejected with the required error.
        // Acceptance criteria: the operation must throw ConfigurationYamlException, because this condition proves that
        // parse rejects invalid fixtures for expected reason.
        var exception = Assert.Throws<ConfigurationYamlException>(
            () => ConfigurationYaml.ParseToJson(yaml, kind));

        // Expected outcome: `exception!.Category` has the required value.
        // Acceptance criteria: `exception!.Category` must equal `expectedError`, because this condition proves that
        // parse rejects invalid fixtures for expected reason.
        Assert.That(exception!.Category, Is.EqualTo(expectedError));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that parse rejects duplicate keys custom tags multiple documents and excessive depth.
    /// Description: Arranges the inputs for parse rejects duplicate keys custom tags multiple documents and excessive depth, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void Parse_RejectsDuplicateKeysCustomTagsMultipleDocumentsAndExcessiveDepth()
    {
        var cases = new[]
        {
            ($"schemaVersion: 1{Environment.NewLine}sources: []\nsources: []{Environment.NewLine}", ConfigurationYamlError.UnsupportedFeature),
            ($"schemaVersion: 1{Environment.NewLine}sources: !custom []{Environment.NewLine}", ConfigurationYamlError.UnsupportedFeature),
            ($"schemaVersion: 1{Environment.NewLine}sources: []{Environment.NewLine}---\nschemaVersion: 1\nsources: []{Environment.NewLine}", ConfigurationYamlError.MultipleDocuments),
            (
                $"schemaVersion: 1{Environment.NewLine}sources:{Environment.NewLine}  - id: source{Environment.NewLine}    name: source{Environment.NewLine}    enabled: true{Environment.NewLine}    kind: http_json{Environment.NewLine}    connection:{Environment.NewLine}{new string(' ', 8)}nested: [{string.Concat(Enumerable.Repeat("[", 21))}0{string.Concat(Enumerable.Repeat("]", 21))}]{Environment.NewLine}    tls: {{verifyServerCertificate: true}}{Environment.NewLine}    timeouts: {{connectMilliseconds: 100}}{Environment.NewLine}",
                ConfigurationYamlError.ExcessiveNesting)
        };

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // parse rejects duplicate keys custom tags multiple documents and excessive depth.
        Assert.Multiple(() =>
        {
            foreach (var (yaml, expected) in cases)
            {
                // Expected outcome: The invalid operation is rejected with the required error.
                // Acceptance criteria: the operation must throw ConfigurationYamlException, because this condition proves that
                // parse rejects duplicate keys custom tags multiple documents and excessive depth.
                var exception = Assert.Throws<ConfigurationYamlException>(
                    () => ConfigurationYaml.ParseToJson(
                        Encoding.UTF8.GetBytes(yaml),
                        ConfigurationKind.PointSources));

                // Expected outcome: `exception!.Category` has the required value.
                // Acceptance criteria: `exception!.Category` must equal `expected`, because this condition proves that
                // parse rejects duplicate keys custom tags multiple documents and excessive depth.
                Assert.That(exception!.Category, Is.EqualTo(expected));
            }
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that parse rejects oversized input before parsing.
    /// Description: Arranges the inputs for parse rejects oversized input before parsing, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void Parse_RejectsOversizedInputBeforeParsing()
    {
        var yaml = new byte[ConfigurationYaml.MaximumBytes + 1];

        // Expected outcome: The invalid operation is rejected with the required error.
        // Acceptance criteria: the operation must throw ConfigurationYamlException, because this condition proves that
        // parse rejects oversized input before parsing.
        var exception = Assert.Throws<ConfigurationYamlException>(
            () => ConfigurationYaml.ParseToJson(yaml, ConfigurationKind.Points));

        // Expected outcome: `exception!.Category` has the required value.
        // Acceptance criteria: `exception!.Category` must equal `ConfigurationYamlError.TooLarge`, because this condition proves that
        // parse rejects oversized input before parsing.
        Assert.That(exception!.Category, Is.EqualTo(ConfigurationYamlError.TooLarge));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that typed parse and render preserve point source contract.
    /// Description: Arranges the inputs for typed parse and render preserve point source contract, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void TypedParseAndRender_PreservePointSourceContract()
    {
        var yaml = File.ReadAllBytes(Path.Combine(FixtureRoot, "point-sources/v1.yaml"));
        var document = ConfigurationYaml.Parse<PointSourceDocument>(
            yaml,
            ConfigurationKind.PointSources);

        var renderedText = ConfigurationYaml.Render(document);

        // Expected outcome: `renderedText` uses the required serialized structure.
        // Acceptance criteria: `renderedText` must match the required boundary text `$"schemaVersion: 1{Environment.NewLine}sources:{Environment.NewLine}"`, because this condition proves that
        // typed parse and render preserve point source contract.
        Assert.That(renderedText, Does.StartWith($"schemaVersion: 1{Environment.NewLine}sources:{Environment.NewLine}"));

        // Expected outcome: `renderedText` includes the required content.
        // Acceptance criteria: `renderedText` must contain `$"{Environment.NewLine}- id:"`, because this condition proves that
        // typed parse and render preserve point source contract.
        Assert.That(renderedText, Does.Contain($"{Environment.NewLine}- id:"));

        // Expected outcome: The observed result satisfies the required contract.
        // Acceptance criteria: the asserted condition must hold, because this condition proves that
        // typed parse and render preserve point source contract.
        Assert.That(renderedText.TrimStart(), Does.Not.StartWith("{"));

        var rendered = Encoding.UTF8.GetBytes(renderedText);
        var reparsed = ConfigurationYaml.Parse<PointSourceDocument>(
            rendered,
            ConfigurationKind.PointSources);

        var originalJson = JsonSerializer.SerializeToNode(document, FlowControlJson.Options);
        var reparsedJson = JsonSerializer.SerializeToNode(reparsed, FlowControlJson.Options);

        // Expected outcome: `JsonNode.DeepEquals(reparsedJson` confirms the required condition.
        // Acceptance criteria: `JsonNode.DeepEquals(reparsedJson` must be true, because this condition proves that
        // typed parse and render preserve point source contract.
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
            if (!value.ContainsKey("capabilities"))
            {
                value.Remove("revision");
            }

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