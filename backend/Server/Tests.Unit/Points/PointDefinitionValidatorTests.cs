using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text;
using System.Text.Json.Nodes;

namespace Tests.Unit.Points;

[TestFixture]
internal sealed class PointDefinitionValidatorTests
{
    private IPointDefinitionValidator _validator = null!;
    private IReadOnlyDictionary<string, PointSource> _sources = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPointDefinitionValidator, PointDefinitionValidator>();
        _validator = services.BuildServiceProvider()
            .GetRequiredService<IPointDefinitionValidator>();
        _sources = new Dictionary<string, PointSource>(StringComparer.Ordinal)
        {
            ["ha"] = Source("ha", "home_assistant"),
            ["mqtt"] = Source("mqtt", "mqtt"),
            ["http"] = Source("http", "http_json")
        };
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that contract fixture validates and produces typed mappings.
    /// Description: Arranges the inputs for contract fixture validates and produces typed mappings, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void ContractFixture_ValidatesAndProducesTypedMappings()
    {
        var yaml = File.ReadAllBytes(Fixture("points", "v1.yaml"));

        var document = ConfigurationYaml.Parse<PointDocument>(
            yaml,
            ConfigurationKind.Points);

        var sources = ConfigurationYaml.Parse<PointSourceDocument>(
                File.ReadAllBytes(Fixture("point-sources", "v1.yaml")),
                ConfigurationKind.PointSources)
            .Sources.ToDictionary(source => source.Id, StringComparer.Ordinal);

        // Expected outcome: The supported operation is accepted.
        // Acceptance criteria: the operation must complete without throwing an exception, because this condition proves that
        // contract fixture validates and produces typed mappings.
        Assert.That(
            () => _validator.ValidateDocument(document, sources),
            Throws.Nothing);

        var groups = document.Groups.ToDictionary(group => group.Id, StringComparer.Ordinal);
        var validated = document.Points.Select(point =>
            _validator.Validate(point, new PointValidationContext(groups, sources))).ToArray();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // contract fixture validates and produces typed mappings.
        Assert.Multiple(() =>
        {
            // Expected outcome: `validated[0].Mapping` has the required runtime type.
            // Acceptance criteria: `validated[0].Mapping` must be a HttpJsonPointMapping, because this condition proves that
            // contract fixture validates and produces typed mappings.
            Assert.That(validated[0].Mapping, Is.TypeOf<HttpJsonPointMapping>());

            // Expected outcome: `validated[1].Mapping` has the required runtime type.
            // Acceptance criteria: `validated[1].Mapping` must be a HomeAssistantPointMapping, because this condition proves that
            // contract fixture validates and produces typed mappings.
            Assert.That(validated[1].Mapping, Is.TypeOf<HomeAssistantPointMapping>());

            // Expected outcome: `validated[2].MultiStateLabels` contains the required number of entries.
            // Acceptance criteria: `validated[2].MultiStateLabels` must contain exactly 3 entries, because this condition proves that
            // contract fixture validates and produces typed mappings.
            Assert.That(validated[2].MultiStateLabels, Has.Count.EqualTo(3));

            // Expected outcome: `validated[3].Limits?.Maximum` has the required value.
            // Acceptance criteria: `validated[3].Limits?.Maximum` must equal `9_007_199_254_740_991`, because this condition proves that
            // contract fixture validates and produces typed mappings.
            Assert.That(validated[3].Limits?.Maximum, Is.EqualTo(9_007_199_254_740_991));

            // Expected outcome: `validated[4].Mapping` has the required runtime type.
            // Acceptance criteria: `validated[4].Mapping` must be a MqttPointMapping, because this condition proves that
            // contract fixture validates and produces typed mappings.
            Assert.That(validated[4].Mapping, Is.TypeOf<MqttPointMapping>());
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that bound direction capability combinations are accepted.
    /// Description: Arranges the inputs for bound direction capability combinations are accepted, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [TestCase(DataDirection.Input, true, false)]
    [TestCase(DataDirection.Output, false, true)]
    [TestCase(DataDirection.Output, true, true)]
    [TestCase(DataDirection.InputOutput, true, true)]
    public void BoundDirectionCapabilityCombinations_AreAccepted(
        DataDirection direction,
        bool readable,
        bool commandable)
    {
        var point = BoundPoint(direction, readable, commandable) with
        {
            SafeDisablePolicy = commandable ? Safety() : null,
        };

        // Expected outcome: The supported operation is accepted.
        // Acceptance criteria: the operation must complete without throwing an exception, because this condition proves that
        // bound direction capability combinations are accepted.
        Assert.That(
            () => _validator.Validate(point, Context()),
            Throws.Nothing);
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that invalid bound direction capability combinations are rejected.
    /// Description: Arranges the inputs for invalid bound direction capability combinations are rejected, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [TestCase(DataDirection.Input, false, false)]
    [TestCase(DataDirection.Input, true, true)]
    [TestCase(DataDirection.Output, true, false)]
    [TestCase(DataDirection.InputOutput, true, false)]
    [TestCase(DataDirection.Value, true, true)]
    public void InvalidBoundDirectionCapabilityCombinations_AreRejected(
        DataDirection direction,
        bool readable,
        bool commandable)
    {
        var point = BoundPoint(direction, readable, commandable) with
        {
            SafeDisablePolicy = commandable ? Safety() : null,
        };

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
        // invalid bound direction capability combinations are rejected.
        Assert.That(
            () => _validator.Validate(point, Context()),
            Throws.TypeOf<PointDefinitionValidationException>());
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that virtual retained values are type and range checked.
    /// Description: Arranges the inputs for virtual retained values are type and range checked, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void VirtualRetainedValues_AreTypeAndRangeChecked()
    {
        var valid = VirtualPoint(PointValueType.Integer) with
        {
            Persistence = "retained",
            RelinquishDefault = JsonValue.Create(4),
            Limits = new JsonObject { ["minimum"] = 0, ["maximum"] = 10 },
        };
        var invalid = valid with { RelinquishDefault = JsonValue.Create(10.5) };

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // virtual retained values are type and range checked.
        Assert.Multiple(() =>
        {
            // Expected outcome: The supported operation is accepted.
            // Acceptance criteria: the operation must complete without throwing an exception, because this condition proves that
            // virtual retained values are type and range checked.
            Assert.That(() => _validator.Validate(valid, Context()), Throws.Nothing);

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
            // virtual retained values are type and range checked.
            Assert.That(
                () => _validator.Validate(invalid, Context()),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that analog non finite values are rejected.
    /// Description: Arranges the inputs for analog non finite values are rejected, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void AnalogNonFiniteValues_AreRejected(double value)
    {
        var point = VirtualPoint(PointValueType.Analog) with
        {
            Persistence = "retained",
            RelinquishDefault = JsonValue.Create(value),
        };

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
        // analog non finite values are rejected.
        Assert.That(
            () => _validator.Validate(point, Context()),
            Throws.TypeOf<PointDefinitionValidationException>());
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that digital and multi state labels are strict.
    /// Description: Arranges the inputs for digital and multi state labels are strict, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void DigitalAndMultiStateLabels_AreStrict()
    {
        var duplicateDigital = VirtualPoint(PointValueType.Digital) with
        {
            StateLabels = new JsonObject { ["false"] = "Off", ["true"] = "off" },
        };
        var duplicateState = VirtualPoint(PointValueType.MultiState) with
        {
            StateLabels = new JsonArray
            {
                new JsonObject { ["key"] = "off", ["label"] = "Off" },
                new JsonObject { ["key"] = "off", ["label"] = "On" }
            },
        };

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // digital and multi state labels are strict.
        Assert.Multiple(() =>
        {
            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
            // digital and multi state labels are strict.
            Assert.That(
                () => _validator.Validate(duplicateDigital, Context()),
                Throws.TypeOf<PointDefinitionValidationException>());

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
            // digital and multi state labels are strict.
            Assert.That(
                () => _validator.Validate(duplicateState, Context()),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that text requires positive maximum length.
    /// Description: Arranges the inputs for text requires positive maximum length, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void TextRequiresPositiveMaximumLength()
    {
        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
        // text requires positive maximum length.
        Assert.That(
            () => _validator.Validate(VirtualPoint(PointValueType.Text), Context()),
            Throws.TypeOf<PointDefinitionValidationException>());
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that bound point resolves inherited source and rejects conflicts.
    /// Description: Arranges the inputs for bound point resolves inherited source and rejects conflicts, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void BoundPoint_ResolvesInheritedSourceAndRejectsConflicts()
    {
        var groups = new Dictionary<string, PointGroup>
        {
            ["group"] = new() { Id = "group", Name = "Group", SourceId = "ha" }
        };
        var inherited = BoundPoint(DataDirection.Input, true, false) with
        {
            GroupId = "group",
            SourceId = null,
            Mapping = new JsonObject { ["entityId"] = "sensor.temperature" },
        };
        var conflicting = inherited with { SourceId = "mqtt" };

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // bound point resolves inherited source and rejects conflicts.
        Assert.Multiple(() =>
        {
            // Expected outcome: the asserted result has the required value.
            // Acceptance criteria: the asserted result must equal `PointSourceKind.HomeAssistant`, because this condition proves that
            // bound point resolves inherited source and rejects conflicts.
            Assert.That(
                _validator.Validate(
                    inherited,
                    new PointValidationContext(groups, _sources)).SourceKind,
                Is.EqualTo(PointSourceKind.HomeAssistant));

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
            // bound point resolves inherited source and rejects conflicts.
            Assert.That(
                () => _validator.Validate(
                    conflicting,
                    new PointValidationContext(groups, _sources)),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that source mappings require capabilities and reject credential literals.
    /// Description: Arranges the inputs for source mappings require capabilities and reject credential literals, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void SourceMappings_RequireCapabilitiesAndRejectCredentialLiterals()
    {
        var missingCommandTopic = BoundPoint(DataDirection.Output, false, true) with
        {
            SourceId = "mqtt",
            Mapping = new JsonObject { ["stateTopic"] = "state" },
            SafeDisablePolicy = Safety(),
        };
        var credential = BoundPoint(DataDirection.Input, true, false) with
        {
            Mapping = new JsonObject
            {
                ["path"] = "/value",
                ["method"] = "GET",
                ["authorization"] = "Bearer literal"
            },
        };

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // source mappings require capabilities and reject credential literals.
        Assert.Multiple(() =>
        {
            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
            // source mappings require capabilities and reject credential literals.
            Assert.That(
                () => _validator.Validate(missingCommandTopic, Context()),
                Throws.TypeOf<PointDefinitionValidationException>());

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
            // source mappings require capabilities and reject credential literals.
            Assert.That(
                () => _validator.Validate(credential, Context()),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that document rejects duplicate names and reserved group name.
    /// Description: Arranges the inputs for document rejects duplicate names and reserved group name, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void DocumentRejectsDuplicateNamesAndReservedGroupName()
    {
        var points = new[]
        {
            VirtualPoint(PointValueType.Analog),
            VirtualPoint(PointValueType.Analog) with { Id = "second", Name = "POINT" }
        };
        var duplicate = new PointDocument { Points = points };
        var reserved = new PointGroup
        {
            Id = "standalone",
            Name = "__standalonepointgroup__"
        };

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // document rejects duplicate names and reserved group name.
        Assert.Multiple(() =>
        {
            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
            // document rejects duplicate names and reserved group name.
            Assert.That(
                () => _validator.ValidateDocument(duplicate, _sources),
                Throws.TypeOf<PointDefinitionValidationException>());

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
            // document rejects duplicate names and reserved group name.
            Assert.That(
                () => _validator.ValidateGroup(reserved, _sources),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that compatibility predicates require exact type and numeric units.
    /// Description: Arranges the inputs for compatibility predicates require exact type and numeric units, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void CompatibilityPredicates_RequireExactTypeAndNumericUnits()
    {
        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // compatibility predicates require exact type and numeric units.
        Assert.Multiple(() =>
        {
            // Expected outcome: `PointCompatibility.CanRead(PointDirection.Input` confirms the required condition.
            // Acceptance criteria: `PointCompatibility.CanRead(PointDirection.Input` must be true, because this condition proves that
            // compatibility predicates require exact type and numeric units.
            Assert.That(PointCompatibility.CanRead(DataDirection.Input), Is.True);

            // Expected outcome: `PointCompatibility.CanCommand(PointDirection.Input` rejects the prohibited condition.
            // Acceptance criteria: `PointCompatibility.CanCommand(PointDirection.Input` must be false, because this condition proves that
            // compatibility predicates require exact type and numeric units.
            Assert.That(PointCompatibility.CanCommand(DataDirection.Input), Is.False);

            // Expected outcome: the asserted result confirms the required condition.
            // Acceptance criteria: the asserted result must be true, because this condition proves that
            // compatibility predicates require exact type and numeric units.
            Assert.That(
                PointCompatibility.ValuesAreCompatible(
                    PointValueType.Analog, "degC", PointValueType.Analog, "degC"),
                Is.True);

            // Expected outcome: the asserted result rejects the prohibited condition.
            // Acceptance criteria: the asserted result must be false, because this condition proves that
            // compatibility predicates require exact type and numeric units.
            Assert.That(
                PointCompatibility.ValuesAreCompatible(
                    PointValueType.Analog, "degC", PointValueType.Analog, "degF"),
                Is.False);

            // Expected outcome: the asserted result rejects the prohibited condition.
            // Acceptance criteria: the asserted result must be false, because this condition proves that
            // compatibility predicates require exact type and numeric units.
            Assert.That(
                PointCompatibility.ValuesAreCompatible(
                    PointValueType.Digital, null, PointValueType.Analog, null),
                Is.False);
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that arbitrary yaml and json validation never leaks unexpected exceptions.
    /// Description: Arranges the inputs for arbitrary yaml and json validation never leaks unexpected exceptions, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public void ArbitraryYamlAndJsonValidation_NeverLeaksUnexpectedExceptions()
    {
        var random = new Random(17);
        for (var index = 0; index < 500; index++)
        {
            var bytes = new byte[random.Next(0, 512)];
            random.NextBytes(bytes);
            Assert.DoesNotThrow(() =>
            {
                try
                {
                    var document = ConfigurationYaml.Parse<PointDocument>(
                        bytes,
                        ConfigurationKind.Points);
                    _validator.ValidateDocument(document, _sources);
                }
                catch (Exception exception) when (
                    exception is ConfigurationYamlException
                        or PointDefinitionValidationException
                        or DecoderFallbackException
                        or System.Text.Json.JsonException)
                {
                }
            });
        }
    }

    private PointValidationContext Context() =>
        new(
            new Dictionary<string, PointGroup>(StringComparer.Ordinal),
            _sources);

    private static Point BoundPoint(
        DataDirection direction,
        bool readable,
        bool commandable)
    {
        var mqttMapping = new JsonObject();
        if (readable)
        {
            mqttMapping["stateTopic"] = "point/state";
        }

        if (commandable)
        {
            mqttMapping["commandTopic"] = "point/command";
        }

        return new Point
        {
            Id = "point",
            Name = "Point",
            Enabled = true,
            Implementation = "bound",
            Direction = direction,
            ValueType = PointValueType.Analog,
            Readable = readable,
            Commandable = commandable,
            Persistence = "volatile",
            SourceId = "mqtt",
            Mapping = mqttMapping
        };
    }

    private static Point VirtualPoint(PointValueType valueType) => new()
    {
        Id = "point",
        Name = "Point",
        Enabled = true,
        Implementation = "virtual",
        Direction = DataDirection.Value,
        ValueType = valueType,
        StateLabels = valueType switch
        {
            PointValueType.Digital => new JsonObject { ["false"] = "Off", ["true"] = "On" },
            PointValueType.MultiState => new JsonArray
            {
                new JsonObject { ["key"] = "off", ["label"] = "Off" },
                new JsonObject { ["key"] = "on", ["label"] = "On" }
            },
            _ => null,
        },
        Readable = true,
        Commandable = false,
        Persistence = "volatile"
    };

    private static JsonObject Safety() => new()
    {
        ["startup"] = "relinquish",
        ["shutdown"] = "relinquish",
        ["communicationLoss"] = "relinquish",
        ["disable"] = "relinquish"
    };

    private static PointSource Source(string id, string kind) => new()
    {
        Id = id,
        Name = id,
        Enabled = true,
        Kind = kind,
        Connection = new PointSourceConnection()
    };

    private static string Fixture(params string[] parts) =>
        Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "ContractFixtures",
            Path.Combine(parts));
}