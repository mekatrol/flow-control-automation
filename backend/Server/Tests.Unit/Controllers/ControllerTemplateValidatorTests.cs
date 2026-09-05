using Server.Common;
using Server.Common.Models;
using Server.Common.Services;
using Server.Common.Types;
using Server.Compiler.Services.Implementation;
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
    private ControllerTemplateValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new ControllerTemplateValidator();
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that constrained fixture parses and validates as typed capabilities.
    /// Description: Arranges the inputs for constrained fixture parses and validates as typed capabilities, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void ConstrainedFixture_ParsesAndValidatesAsTypedCapabilities()
    {
        var template = ControllerTemplateYaml.Parse(
            File.ReadAllBytes(Fixture("constrained.v1.yaml")));

        var validated = _validator.Validate(template);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // constrained fixture parses and validates as typed capabilities.
        Assert.Multiple(() =>
        {
            // Expected outcome: `validated.PointTypes` contains the required values.
            // Acceptance criteria: `validated.PointTypes` must be equivalent to `[PointValueType.Digital]`, because this condition proves that
            // constrained fixture parses and validates as typed capabilities.
            Assert.That(validated.PointTypes, Is.EquivalentTo([FlowPointValueType.Digital]));

            // Expected outcome: `validated.PointDirections` contains the required values.
            // Acceptance criteria: `validated.PointDirections` must be equivalent to `[PointDirection.Input, PointDirection.Output]`, because this condition proves that
            // constrained fixture parses and validates as typed capabilities.
            Assert.That(
                validated.PointDirections,
                Is.EquivalentTo([DataDirectionType.Input, DataDirectionType.Output]));

            // Expected outcome: `validated.FlowFunctions` includes the required content.
            // Acceptance criteria: `validated.FlowFunctions` must contain `"readPoint"`, because this condition proves that
            // constrained fixture parses and validates as typed capabilities.
            Assert.That(validated.FlowFunctions, Does.Contain(FlowFunctionType.ReadPoint));

            // Expected outcome: `validated.ExecutionModes` contains the required values.
            // Acceptance criteria: `validated.ExecutionModes` must be equivalent to `[ExecutionModeType.Interval]`, because this condition proves that
            // constrained fixture parses and validates as typed capabilities.
            Assert.That(
                validated.ExecutionModes,
                Is.EquivalentTo([ExecutionModeType.Interval]));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that default is read only and exhaustive.
    /// Description: Arranges the inputs for default is read only and exhaustive, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void Default_IsReadOnlyAndExhaustive()
    {
        var validated = _validator.Validate(
            BuiltInControllerTemplate.Default,
            allowBuiltInDefault: true);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // default is read only and exhaustive.
        Assert.Multiple(() =>
        {
            // Expected outcome: `validated.Source.Id` has the required value.
            // Acceptance criteria: `validated.Source.Id` must equal `"default"`, because this condition proves that
            // default is read only and exhaustive.
            Assert.That(validated.Source.Id, Is.EqualTo("default"));

            // Expected outcome: `validated.Source.ReadOnly` confirms the required condition.
            // Acceptance criteria: `validated.Source.ReadOnly` must be true, because this condition proves that
            // default is read only and exhaustive.
            Assert.That(validated.Source.ReadOnly, Is.True);

            // Expected outcome: `validated.PointTypes` contains the required values.
            // Acceptance criteria: `validated.PointTypes` must be equivalent to `Enum.GetValues<PointValueType>(`, because this condition proves that
            // default is read only and exhaustive.
            Assert.That(validated.PointTypes, Is.EquivalentTo(Enum.GetValues<FlowPointValueType>()));

            // Expected outcome: `validated.PointDirections` contains the required values.
            // Acceptance criteria: `validated.PointDirections` must be equivalent to `Enum.GetValues<PointDirection>(`, because this condition proves that
            // default is read only and exhaustive.
            Assert.That(
                validated.PointDirections,
                Is.EquivalentTo(Enum.GetValues<DataDirectionType>()));

            // Expected outcome: `validated.PointFeatures` contains the required values.
            // Acceptance criteria: `validated.PointFeatures` must be equivalent to `Enum.GetValues<ControllerPointFeatureType>(`, because this condition proves that
            // default is read only and exhaustive.
            Assert.That(
                validated.PointFeatures,
                Is.EquivalentTo(Enum.GetValues<ControllerPointFeatureType>()));

            // Expected outcome: `validated.ConnectorDataTypes` contains the required values.
            // Acceptance criteria: `validated.ConnectorDataTypes` must be equivalent to `Enum.GetValues<ConnectorDataType>(`, because this condition proves that
            // default is read only and exhaustive.
            Assert.That(
                validated.ConnectorDataTypes,
                Is.EquivalentTo(Enum.GetValues<ConnectorDataType>()));

            // Expected outcome: `validated.ExecutionModes` contains the required values.
            // Acceptance criteria: `validated.ExecutionModes` must be equivalent to `Enum.GetValues<ExecutionModeType>(`, because this condition proves that
            // default is read only and exhaustive.
            Assert.That(
                validated.ExecutionModes,
                Is.EquivalentTo(Enum.GetValues<ExecutionModeType>()));

            // Expected outcome: `validated.RuntimeFeatures` contains the required values.
            // Acceptance criteria: `validated.RuntimeFeatures` must be equivalent to `Enum.GetValues<ControllerRuntimeFeatureType>(`, because this condition proves that
            // default is read only and exhaustive.
            Assert.That(
                validated.RuntimeFeatures,
                Is.EquivalentTo(Enum.GetValues<ControllerRuntimeFeatureType>()));

            // Expected outcome: `validated.FlowFunctions` contains the required values.
            // Acceptance criteria: `validated.FlowFunctions` must be equivalent to `FlowNodeRegistry.Functions`, because this condition proves that
            // default is read only and exhaustive.
            Assert.That(validated.FlowFunctions, Is.EquivalentTo(FlowNodeRegistry.Functions));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that default matches embedded contract fixture.
    /// Description: Arranges the inputs for default matches embedded contract fixture, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void Default_MatchesEmbeddedContractFixture()
    {
        var fixture = ControllerTemplateYaml.Parse(
            File.ReadAllBytes(Fixture("default.v1.yaml")));

        var builtInJson = JsonSerializer.SerializeToNode(
            BuiltInControllerTemplate.Default,
            FlowControlJson.Options);
        var fixtureJson = JsonSerializer.SerializeToNode(fixture, FlowControlJson.Options);

        // Expected outcome: `JsonNode.DeepEquals(builtInJson` confirms the required condition.
        // Acceptance criteria: `JsonNode.DeepEquals(builtInJson` must be true, because this condition proves that
        // default matches embedded contract fixture.
        Assert.That(
            JsonNode.DeepEquals(builtInJson, fixtureJson),
            Is.True,
            $"Built-in: {builtInJson}{Environment.NewLine}Fixture: {fixtureJson}");
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that unsupported capability enums are rejected with paths.
    /// Description: Arranges the inputs for unsupported capability enums are rejected with paths, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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
            "pointTypes" => capabilities with { PointTypes = [(FlowPointValueType)byte.MaxValue] },
            "pointDirections" => capabilities with { PointDirections = [(DataDirectionType)byte.MaxValue] },
            "pointFeatures" => capabilities with { PointFeatures = [(ControllerPointFeatureType)byte.MaxValue] },
            "connectorDataTypes" => capabilities with { ConnectorDataTypes = [(ConnectorDataType)byte.MaxValue] },
            "flowFunctions" => capabilities with { FlowFunctions = [(FlowFunctionType)byte.MaxValue] },
            "executionModes" => capabilities with { ExecutionModes = [(ExecutionModeType)byte.MaxValue] },
            "runtimeFeatures" => capabilities with { RuntimeFeatures = [(ControllerRuntimeFeatureType)byte.MaxValue] },
            _ => throw new ArgumentOutOfRangeException(nameof(capability)),
        };

        // Expected outcome: The invalid operation is rejected with the required error.
        // Acceptance criteria: the operation must throw ControllerTemplateValidationException, because this condition proves that
        // unsupported capability enums are rejected with paths.
        var exception = Assert.Throws<ControllerTemplateValidationException>(
            () => _validator.Validate(Template() with { Capabilities = capabilities }));

        // Expected outcome: `exception!.Diagnostics.Select(diagnostic => diagnostic.Path` includes the required content.
        // Acceptance criteria: `exception!.Diagnostics.Select(diagnostic => diagnostic.Path` must contain `$"capabilities.{capability}[0]"`, because this condition proves that
        // unsupported capability enums are rejected with paths.
        Assert.That(
            exception!.Diagnostics.Select(diagnostic => diagnostic.Path),
            Does.Contain($"capabilities.{capability}[0]"));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that duplicate capabilities are rejected.
    /// Description: Arranges the inputs for duplicate capabilities are rejected, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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
            "pointTypes" => capabilities with { PointTypes = [FlowPointValueType.Digital, FlowPointValueType.Digital] },
            "pointDirections" => capabilities with { PointDirections = [DataDirectionType.Input, DataDirectionType.Input] },
            "pointFeatures" => capabilities with { PointFeatures = [ControllerPointFeatureType.Read, ControllerPointFeatureType.Read] },
            "connectorDataTypes" => capabilities with
            {
                ConnectorDataTypes = [ConnectorDataType.Boolean, ConnectorDataType.Boolean],
            },
            "flowFunctions" => capabilities with { FlowFunctions = [FlowFunctionType.And, FlowFunctionType.And] },
            "executionModes" => capabilities with { ExecutionModes = [ExecutionModeType.Interval, ExecutionModeType.Interval] },
            "runtimeFeatures" => capabilities with
            {
                RuntimeFeatures = [ControllerRuntimeFeatureType.BoundPoints, ControllerRuntimeFeatureType.BoundPoints],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(capability)),
        };

        // Expected outcome: The invalid operation is rejected with the required error.
        // Acceptance criteria: the operation must throw ControllerTemplateValidationException, because this condition proves that
        // duplicate capabilities are rejected.
        var exception = Assert.Throws<ControllerTemplateValidationException>(
            () => _validator.Validate(Template() with { Capabilities = capabilities }));

        // Expected outcome: The observed result satisfies the required contract.
        // Acceptance criteria: the asserted condition must hold, because this condition proves that
        // duplicate capabilities are rejected.
        Assert.That(
            exception!.Diagnostics,
            Has.Some.Property("Code").EqualTo("duplicate_capability"));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that non positive limits are rejected.
    /// Description: Arranges the inputs for non positive limits are rejected, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: The invalid operation is rejected with the required error.
        // Acceptance criteria: the operation must throw ControllerTemplateValidationException, because this condition proves that
        // non positive limits are rejected.
        var exception = Assert.Throws<ControllerTemplateValidationException>(
            () => _validator.Validate(template));

        // Expected outcome: `exception!.Diagnostics` contains the required number of entries.
        // Acceptance criteria: `exception!.Diagnostics` must contain exactly 4 entries, because this condition proves that
        // non positive limits are rejected.
        Assert.That(exception!.Diagnostics, Has.Count.EqualTo(4));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that invalid ids are rejected.
    /// Description: Arranges the inputs for invalid ids are rejected, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [TestCase("Default")]
    [TestCase("two words")]
    [TestCase(" spaced")]
    [TestCase("9controller")]
    public void InvalidIds_AreRejected(string id)
    {
        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw ControllerTemplateValidationException, because this condition proves that
        // invalid ids are rejected.
        Assert.That(
            () => _validator.Validate(Template() with { Id = id }),
            Throws.TypeOf<ControllerTemplateValidationException>());
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that default identity and read only state are reserved.
    /// Description: Arranges the inputs for default identity and read only state are reserved, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void DefaultIdentityAndReadOnlyState_AreReserved()
    {
        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // default identity and read only state are reserved.
        Assert.Multiple(() =>
        {
            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw ControllerTemplateValidationException, because this condition proves that
            // default identity and read only state are reserved.
            Assert.That(
                () => _validator.Validate(Template() with { Id = "default" }),
                Throws.TypeOf<ControllerTemplateValidationException>());

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw ControllerTemplateValidationException, because this condition proves that
            // default identity and read only state are reserved.
            Assert.That(
                () => _validator.Validate(Template() with { ReadOnly = true }),
                Throws.TypeOf<ControllerTemplateValidationException>());
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that capability predicates use typed validated sets.
    /// Description: Arranges the inputs for capability predicates use typed validated sets, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void CapabilityPredicates_UseTypedValidatedSets()
    {
        var template = _validator.Validate(Template());

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // capability predicates use typed validated sets.
        Assert.Multiple(() =>
        {
            // Expected outcome: the asserted result confirms the required condition.
            // Acceptance criteria: the asserted result must be true, because this condition proves that
            // capability predicates use typed validated sets.
            Assert.That(
                ControllerCapabilitiesSupport.SupportsPoint(
                    template,
                    FlowPointValueType.Digital,
                    DataDirectionType.Input),
                Is.True);

            // Expected outcome: the asserted result rejects the prohibited condition.
            // Acceptance criteria: the asserted result must be false, because this condition proves that
            // capability predicates use typed validated sets.
            Assert.That(
                ControllerCapabilitiesSupport.SupportsPoint(
                    template,
                    FlowPointValueType.Analog,
                    DataDirectionType.Input),
                Is.False);

            // Expected outcome: the asserted result confirms the required condition.
            // Acceptance criteria: the asserted result must be true, because this condition proves that
            // capability predicates use typed validated sets.
            Assert.That(
                ControllerCapabilitiesSupport.SupportsPointFeature(
                    template,
                    ControllerPointFeatureType.Read),
                Is.True);

            // Expected outcome: the asserted result confirms the required condition.
            // Acceptance criteria: the asserted result must be true, because this condition proves that
            // capability predicates use typed validated sets.
            Assert.That(
                ControllerCapabilitiesSupport.SupportsConnector(
                    template,
                    ConnectorDataType.Boolean),
                Is.True);

            // Expected outcome: `ControllerCapabilitiesSupport.SupportsFunction(template` confirms the required condition.
            // Acceptance criteria: `ControllerCapabilitiesSupport.SupportsFunction(template` must be true, because this condition proves that
            // capability predicates use typed validated sets.
            Assert.That(
                ControllerCapabilitiesSupport.SupportsFunction(template, FlowFunctionType.And),
                Is.True);

            // Expected outcome: the asserted result rejects the prohibited condition.
            // Acceptance criteria: the asserted result must be false, because this condition proves that
            // capability predicates use typed validated sets.
            Assert.That(
                ControllerCapabilitiesSupport.SupportsExecutionMode(
                    template,
                    ExecutionModeType.Event),
                Is.False);

            // Expected outcome: the asserted result confirms the required condition.
            // Acceptance criteria: the asserted result must be true, because this condition proves that
            // capability predicates use typed validated sets.
            Assert.That(
                ControllerCapabilitiesSupport.SupportsRuntimeFeature(
                    template,
                    ControllerRuntimeFeatureType.BoundPoints),
                Is.True);
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that strict yaml rejects aliases tags size and nesting.
    /// Description: Arranges the inputs for strict yaml rejects aliases tags size and nesting, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void StrictYaml_RejectsAliasesTagsSizeAndNesting()
    {
        var cases = new[]
        {
            $"schemaVersion: 1{Environment.NewLine}id: controller{Environment.NewLine}name: Controller\ncapabilities: &cap {{}}{Environment.NewLine}limits: *cap{Environment.NewLine}",
            $"schemaVersion: 1{Environment.NewLine}id: controller{Environment.NewLine}name: !custom Controller\ncapabilities: {{}}\nlimits: {{}}{Environment.NewLine}",
            $"schemaVersion: 1{Environment.NewLine}id: controller{Environment.NewLine}name: Controller\ncapabilities:{Environment.NewLine}  pointTypes: [{string.Concat(Enumerable.Repeat("[", 21))}digital{string.Concat(Enumerable.Repeat("]", 21))}]{Environment.NewLine}limits: {{}}{Environment.NewLine}"
        };

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // strict yaml rejects aliases tags size and nesting.
        Assert.Multiple(() =>
        {
            foreach (var yaml in cases)
            {
                // Expected outcome: The invalid operation is rejected.
                // Acceptance criteria: the operation must throw ConfigurationYamlException, because this condition proves that
                // strict yaml rejects aliases tags size and nesting.
                Assert.That(
                    () => ControllerTemplateYaml.Parse(Encoding.UTF8.GetBytes(yaml)),
                    Throws.TypeOf<ConfigurationYamlException>());
            }

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw ConfigurationYamlException, because this condition proves that
            // strict yaml rejects aliases tags size and nesting.
            Assert.That(
                () => ControllerTemplateYaml.Parse(
                    new byte[ConfigurationYaml.MaximumBytes + 1]),
                Throws.TypeOf<ConfigurationYamlException>());
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that arbitrary yaml parsing and validation never leaks unexpected exceptions.
    /// Description: Arranges the inputs for arbitrary yaml parsing and validation never leaks unexpected exceptions, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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
        PointTypes = [FlowPointValueType.Digital],
        PointDirections = [DataDirectionType.Input, DataDirectionType.Output],
        PointFeatures = [ControllerPointFeatureType.Read, ControllerPointFeatureType.Command],
        ConnectorDataTypes = [ConnectorDataType.Boolean],
        FlowFunctions = [FlowFunctionType.And, FlowFunctionType.ReadPoint, FlowFunctionType.WritePoint],
        ExecutionModes = [ExecutionModeType.Interval],
        RuntimeFeatures = [ControllerRuntimeFeatureType.BoundPoints]
    };

    private static string Fixture(string file) =>
        Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "ContractFixtures",
            "controllers",
            file);
}