using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tests.Unit.Controllers;

[TestFixture]
internal sealed class ControllerTemplateValidatorTests
{
    private IControllerTemplateValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new ControllerTemplateValidator();
    }

    [Test]
    public void ConstrainedFixture_ParsesAndValidatesAsTypedCapabilities()
    {
        var template = ControllerTemplateYaml.Parse(
            File.ReadAllBytes(Fixture("constrained.v1.yaml")));

        var validated = _validator.Validate(template);

        Assert.Multiple(() =>
        {
            Assert.That(validated.PointTypes, Is.EquivalentTo([PointValueType.Digital]));
            Assert.That(
                validated.PointDirections,
                Is.EquivalentTo([PointDirection.Input, PointDirection.Output]));
            Assert.That(validated.FlowFunctions, Does.Contain("read-point"));
            Assert.That(
                validated.ExecutionModes,
                Is.EquivalentTo([ExecutionMode.Interval]));
        });
    }

    [Test]
    public void Default_IsReadOnlyAndExhaustive()
    {
        var validated = _validator.Validate(
            BuiltInControllerTemplate.Default,
            allowBuiltInDefault: true);

        Assert.Multiple(() =>
        {
            Assert.That(validated.Source.Id, Is.EqualTo("default"));
            Assert.That(validated.Source.ReadOnly, Is.True);
            Assert.That(validated.PointTypes, Is.EquivalentTo(Enum.GetValues<PointValueType>()));
            Assert.That(
                validated.PointDirections,
                Is.EquivalentTo(Enum.GetValues<PointDirection>()));
            Assert.That(
                validated.PointFeatures,
                Is.EquivalentTo(Enum.GetValues<ControllerPointFeature>()));
            Assert.That(
                validated.ConnectorDataTypes,
                Is.EquivalentTo(Enum.GetValues<ConnectorDataType>()));
            Assert.That(
                validated.ExecutionModes,
                Is.EquivalentTo(Enum.GetValues<ExecutionMode>()));
            Assert.That(
                validated.RuntimeFeatures,
                Is.EquivalentTo(Enum.GetValues<ControllerRuntimeFeature>()));
            Assert.That(validated.FlowFunctions, Is.EquivalentTo(FlowNodeRegistry.Functions));
        });
    }

    [Test]
    public void Default_MatchesEmbeddedContractFixture()
    {
        var fixture = ControllerTemplateYaml.Parse(
            File.ReadAllBytes(Fixture("default.v1.yaml")));

        var builtInJson = JsonSerializer.SerializeToNode(
            BuiltInControllerTemplate.Default,
            FlowControlJson.Options);
        var fixtureJson = JsonSerializer.SerializeToNode(fixture, FlowControlJson.Options);
        Assert.That(JsonNode.DeepEquals(builtInJson, fixtureJson), Is.True);
    }

    [TestCase("pointTypes")]
    [TestCase("pointDirections")]
    [TestCase("pointFeatures")]
    [TestCase("connectorDataTypes")]
    [TestCase("flowFunctions")]
    [TestCase("executionModes")]
    [TestCase("runtimeFeatures")]
    public void UnsupportedCapabilityEnums_AreRejectedWithPaths(string capability)
    {
        var capabilities = Capabilities();
        capabilities = capability switch
        {
            "pointTypes" => capabilities with { PointTypes = ["binary"] },
            "pointDirections" => capabilities with { PointDirections = ["sideways"] },
            "pointFeatures" => capabilities with { PointFeatures = ["teleport"] },
            "connectorDataTypes" => capabilities with { ConnectorDataTypes = ["object"] },
            "flowFunctions" => capabilities with { FlowFunctions = ["unknown-node"] },
            "executionModes" => capabilities with { ExecutionModes = ["continuous"] },
            "runtimeFeatures" => capabilities with { RuntimeFeatures = ["magic"] },
            _ => throw new ArgumentOutOfRangeException(nameof(capability)),
        };

        var exception = Assert.Throws<ControllerTemplateValidationException>(
            () => _validator.Validate(Template() with { Capabilities = capabilities }));

        Assert.That(
            exception!.Diagnostics.Select(diagnostic => diagnostic.Path),
            Does.Contain($"capabilities.{capability}[0]"));
    }

    [TestCase("pointTypes")]
    [TestCase("pointDirections")]
    [TestCase("pointFeatures")]
    [TestCase("connectorDataTypes")]
    [TestCase("flowFunctions")]
    [TestCase("executionModes")]
    [TestCase("runtimeFeatures")]
    public void DuplicateCapabilities_AreRejected(string capability)
    {
        var capabilities = Capabilities();
        capabilities = capability switch
        {
            "pointTypes" => capabilities with { PointTypes = ["digital", "digital"] },
            "pointDirections" => capabilities with { PointDirections = ["input", "input"] },
            "pointFeatures" => capabilities with { PointFeatures = ["read", "read"] },
            "connectorDataTypes" => capabilities with
            {
                ConnectorDataTypes = ["boolean", "boolean"],
            },
            "flowFunctions" => capabilities with { FlowFunctions = ["and", "and"] },
            "executionModes" => capabilities with { ExecutionModes = ["interval", "interval"] },
            "runtimeFeatures" => capabilities with
            {
                RuntimeFeatures = ["bound_points", "bound_points"],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(capability)),
        };

        var exception = Assert.Throws<ControllerTemplateValidationException>(
            () => _validator.Validate(Template() with { Capabilities = capabilities }));

        Assert.That(
            exception!.Diagnostics,
            Has.Some.Property("Code").EqualTo("duplicate_capability"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonPositiveLimits_AreRejected(int limit)
    {
        var template = Template() with
        {
            Limits = new ControllerLimits
            {
                MaxFlows = limit,
                MaxNodesPerFlow = limit,
                MaxConnectionsPerFlow = limit,
                MinimumIntervalMilliseconds = limit
            },
        };

        var exception = Assert.Throws<ControllerTemplateValidationException>(
            () => _validator.Validate(template));

        Assert.That(exception!.Diagnostics, Has.Count.EqualTo(4));
    }

    [TestCase("Default")]
    [TestCase("two words")]
    [TestCase(" spaced")]
    [TestCase("9controller")]
    public void InvalidIds_AreRejected(string id)
    {
        Assert.That(
            () => _validator.Validate(Template() with { Id = id }),
            Throws.TypeOf<ControllerTemplateValidationException>());
    }

    [Test]
    public void DefaultIdentityAndReadOnlyState_AreReserved()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => _validator.Validate(Template() with { Id = "default" }),
                Throws.TypeOf<ControllerTemplateValidationException>());
            Assert.That(
                () => _validator.Validate(Template() with { ReadOnly = true }),
                Throws.TypeOf<ControllerTemplateValidationException>());
        });
    }

    [Test]
    public void CapabilityPredicates_UseTypedValidatedSets()
    {
        var template = _validator.Validate(Template());

        Assert.Multiple(() =>
        {
            Assert.That(
                ControllerCapabilitiesSupport.SupportsPoint(
                    template,
                    PointValueType.Digital,
                    PointDirection.Input),
                Is.True);
            Assert.That(
                ControllerCapabilitiesSupport.SupportsPoint(
                    template,
                    PointValueType.Analog,
                    PointDirection.Input),
                Is.False);
            Assert.That(
                ControllerCapabilitiesSupport.SupportsPointFeature(
                    template,
                    ControllerPointFeature.Read),
                Is.True);
            Assert.That(
                ControllerCapabilitiesSupport.SupportsConnector(
                    template,
                    ConnectorDataType.Boolean),
                Is.True);
            Assert.That(
                ControllerCapabilitiesSupport.SupportsFunction(template, "and"),
                Is.True);
            Assert.That(
                ControllerCapabilitiesSupport.SupportsExecutionMode(
                    template,
                    ExecutionMode.Event),
                Is.False);
            Assert.That(
                ControllerCapabilitiesSupport.SupportsRuntimeFeature(
                    template,
                    ControllerRuntimeFeature.BoundPoints),
                Is.True);
        });
    }

    [Test]
    public void StrictYaml_RejectsAliasesTagsSizeAndNesting()
    {
        var cases = new[]
        {
            $"schemaVersion: 1{Environment.NewLine}id: controller{Environment.NewLine}name: Controller\ncapabilities: &cap {{}}{Environment.NewLine}limits: *cap{Environment.NewLine}",
            $"schemaVersion: 1{Environment.NewLine}id: controller{Environment.NewLine}name: !custom Controller\ncapabilities: {{}}\nlimits: {{}}{Environment.NewLine}",
            $"schemaVersion: 1{Environment.NewLine}id: controller{Environment.NewLine}name: Controller\ncapabilities:{Environment.NewLine}  pointTypes: [{string.Concat(Enumerable.Repeat("[", 21))}digital{string.Concat(Enumerable.Repeat("]", 21))}]{Environment.NewLine}limits: {{}}{Environment.NewLine}"
        };

        Assert.Multiple(() =>
        {
            foreach (var yaml in cases)
            {
                Assert.That(
                    () => ControllerTemplateYaml.Parse(Encoding.UTF8.GetBytes(yaml)),
                    Throws.TypeOf<ConfigurationYamlException>());
            }

            Assert.That(
                () => ControllerTemplateYaml.Parse(
                    new byte[ConfigurationYaml.MaximumBytes + 1]),
                Throws.TypeOf<ConfigurationYamlException>());
        });
    }

    [Test]
    public void ArbitraryYamlParsingAndValidation_NeverLeaksUnexpectedExceptions()
    {
        var random = new Random(23);
        for (var index = 0; index < 500; index++)
        {
            var bytes = new byte[random.Next(0, 512)];
            random.NextBytes(bytes);
            Assert.DoesNotThrow(() =>
            {
                try
                {
                    _validator.Validate(ControllerTemplateYaml.Parse(bytes));
                }
                catch (Exception exception) when (
                    exception is ConfigurationYamlException
                        or ControllerTemplateValidationException
                        or DecoderFallbackException)
                {
                }
            });
        }
    }

    private static ControllerTemplate Template() => new()
    {
        Id = "compact",
        Name = "Compact",
        Capabilities = Capabilities(),
        Limits = new ControllerLimits
        {
            MaxFlows = 8,
            MaxNodesPerFlow = 64,
            MaxConnectionsPerFlow = 96,
            MinimumIntervalMilliseconds = 100
        }
    };

    private static ControllerCapabilities Capabilities() => new()
    {
        PointTypes = ["digital"],
        PointDirections = ["input", "output"],
        PointFeatures = ["read", "command"],
        ConnectorDataTypes = ["boolean"],
        FlowFunctions = ["and", "read-point", "write-point"],
        ExecutionModes = ["interval"],
        RuntimeFeatures = ["bound_points"]
    };

    private static string Fixture(string file) =>
        Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "ContractFixtures",
            "controllers",
            file);
}