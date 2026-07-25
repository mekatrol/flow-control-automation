using Microsoft.Extensions.DependencyInjection;
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

        Assert.That(
            () => _validator.ValidateDocument(document, sources),
            Throws.Nothing);

        var groups = document.Groups.ToDictionary(group => group.Id, StringComparer.Ordinal);
        var validated = document.Points.Select(point =>
            _validator.Validate(point, new PointValidationContext(groups, sources))).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(validated[0].Mapping, Is.TypeOf<HttpJsonPointMapping>());
            Assert.That(validated[1].Mapping, Is.TypeOf<HomeAssistantPointMapping>());
            Assert.That(validated[2].MultiStateLabels, Has.Count.EqualTo(3));
            Assert.That(validated[3].Limits?.Maximum, Is.EqualTo(9_007_199_254_740_991));
            Assert.That(validated[4].Mapping, Is.TypeOf<MqttPointMapping>());
        });
    }

    [TestCase("input", true, false)]
    [TestCase("output", false, true)]
    [TestCase("output", true, true)]
    [TestCase("input_output", true, true)]
    public void BoundDirectionCapabilityCombinations_AreAccepted(
        string direction,
        bool readable,
        bool commandable)
    {
        var point = BoundPoint(direction, readable, commandable) with
        {
            SafeDisablePolicy = commandable ? Safety() : null,
        };

        Assert.That(
            () => _validator.Validate(point, Context()),
            Throws.Nothing);
    }

    [TestCase("input", false, false)]
    [TestCase("input", true, true)]
    [TestCase("output", true, false)]
    [TestCase("input_output", true, false)]
    [TestCase("value", true, true)]
    public void InvalidBoundDirectionCapabilityCombinations_AreRejected(
        string direction,
        bool readable,
        bool commandable)
    {
        var point = BoundPoint(direction, readable, commandable) with
        {
            SafeDisablePolicy = commandable ? Safety() : null,
        };

        Assert.That(
            () => _validator.Validate(point, Context()),
            Throws.TypeOf<PointDefinitionValidationException>());
    }

    [Test]
    public void VirtualRetainedValues_AreTypeAndRangeChecked()
    {
        var valid = VirtualPoint("integer") with
        {
            Persistence = "retained",
            RelinquishDefault = JsonValue.Create(4),
            Limits = new JsonObject { ["minimum"] = 0, ["maximum"] = 10 },
        };
        var invalid = valid with { RelinquishDefault = JsonValue.Create(10.5) };

        Assert.Multiple(() =>
        {
            Assert.That(() => _validator.Validate(valid, Context()), Throws.Nothing);
            Assert.That(
                () => _validator.Validate(invalid, Context()),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void AnalogNonFiniteValues_AreRejected(double value)
    {
        var point = VirtualPoint("analog") with
        {
            Persistence = "retained",
            RelinquishDefault = JsonValue.Create(value),
        };

        Assert.That(
            () => _validator.Validate(point, Context()),
            Throws.TypeOf<PointDefinitionValidationException>());
    }

    [Test]
    public void DigitalAndMultiStateLabels_AreStrict()
    {
        var duplicateDigital = VirtualPoint("digital") with
        {
            StateLabels = new JsonObject { ["false"] = "Off", ["true"] = "off" },
        };
        var duplicateState = VirtualPoint("multi_state") with
        {
            StateLabels = new JsonArray
            {
                new JsonObject { ["key"] = "off", ["label"] = "Off" },
                new JsonObject { ["key"] = "off", ["label"] = "On" }
            },
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                () => _validator.Validate(duplicateDigital, Context()),
                Throws.TypeOf<PointDefinitionValidationException>());
            Assert.That(
                () => _validator.Validate(duplicateState, Context()),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    [Test]
    public void TextRequiresPositiveMaximumLength()
    {
        Assert.That(
            () => _validator.Validate(VirtualPoint("text"), Context()),
            Throws.TypeOf<PointDefinitionValidationException>());
    }

    [Test]
    public void BoundPoint_ResolvesInheritedSourceAndRejectsConflicts()
    {
        var groups = new Dictionary<string, PointGroup>
        {
            ["group"] = new() { Id = "group", Name = "Group", SourceId = "ha" }
        };
        var inherited = BoundPoint("input", true, false) with
        {
            GroupId = "group",
            SourceId = null,
            Mapping = new JsonObject { ["entityId"] = "sensor.temperature" },
        };
        var conflicting = inherited with { SourceId = "mqtt" };

        Assert.Multiple(() =>
        {
            Assert.That(
                _validator.Validate(
                    inherited,
                    new PointValidationContext(groups, _sources)).SourceKind,
                Is.EqualTo(PointSourceKind.HomeAssistant));
            Assert.That(
                () => _validator.Validate(
                    conflicting,
                    new PointValidationContext(groups, _sources)),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    [Test]
    public void SourceMappings_RequireCapabilitiesAndRejectCredentialLiterals()
    {
        var missingCommandTopic = BoundPoint("output", false, true) with
        {
            SourceId = "mqtt",
            Mapping = new JsonObject { ["stateTopic"] = "state" },
            SafeDisablePolicy = Safety(),
        };
        var credential = BoundPoint("input", true, false) with
        {
            Mapping = new JsonObject
            {
                ["path"] = "/value",
                ["method"] = "GET",
                ["authorization"] = "Bearer literal"
            },
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                () => _validator.Validate(missingCommandTopic, Context()),
                Throws.TypeOf<PointDefinitionValidationException>());
            Assert.That(
                () => _validator.Validate(credential, Context()),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    [Test]
    public void DocumentRejectsDuplicateNamesAndReservedGroupName()
    {
        var points = new[]
        {
            VirtualPoint("analog"),
            VirtualPoint("analog") with { Id = "second", Name = "POINT" }
        };
        var duplicate = new PointDocument { Points = points };
        var reserved = new PointGroup
        {
            Id = "standalone",
            Name = "__standalonepointgroup__"
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                () => _validator.ValidateDocument(duplicate, _sources),
                Throws.TypeOf<PointDefinitionValidationException>());
            Assert.That(
                () => _validator.ValidateGroup(reserved, _sources),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    [Test]
    public void CompatibilityPredicates_RequireExactTypeAndNumericUnits()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PointCompatibility.CanRead(PointDirection.Input), Is.True);
            Assert.That(PointCompatibility.CanCommand(PointDirection.Input), Is.False);
            Assert.That(
                PointCompatibility.ValuesAreCompatible(
                    PointValueType.Analog, "degC", PointValueType.Analog, "degC"),
                Is.True);
            Assert.That(
                PointCompatibility.ValuesAreCompatible(
                    PointValueType.Analog, "degC", PointValueType.Analog, "degF"),
                Is.False);
            Assert.That(
                PointCompatibility.ValuesAreCompatible(
                    PointValueType.Digital, null, PointValueType.Analog, null),
                Is.False);
        });
    }

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
        string direction,
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
            ValueType = "analog",
            Readable = readable,
            Commandable = commandable,
            Persistence = "volatile",
            SourceId = "mqtt",
            Mapping = mqttMapping
        };
    }

    private static Point VirtualPoint(string valueType) => new()
    {
        Id = "point",
        Name = "Point",
        Enabled = true,
        Implementation = "virtual",
        Direction = "value",
        ValueType = valueType,
        StateLabels = valueType switch
        {
            "digital" => new JsonObject { ["false"] = "Off", ["true"] = "On" },
            "multi_state" => new JsonArray
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