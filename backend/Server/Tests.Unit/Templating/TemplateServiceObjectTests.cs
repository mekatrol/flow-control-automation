using Server.Common.Contracts.Templating;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tests.Unit.Templating;

[TestFixture]
public sealed class TemplateServiceObjectTests
{
    private static readonly JsonSerializerOptions propertyNameCaseInsensitiveSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private enum OperatingMode
    {
        Automatic
    }

    private sealed record RenderedValues(
        int Integer,
        double DoubleValue,
        bool Boolean,
        string Text,
        float FloatValue,
        long LongValue,
        OperatingMode Mode);

    private sealed record Reading(string Name, double Value);

    private sealed record ReadingsPayload(Reading[] Readings);

    private sealed record WriteObjectDocument
    {
        public WriteObjectDefinition WriteObject { get; init; } = new();
    }

    private sealed record WriteObjectDefinition
    {
        public RenderAs Format { get; init; }

        public string Template { get; init; } = string.Empty;
    }

    private static string FixtureRoot => Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "TemplateFixtures");

    private static IEnumerable<object[]> GetPositiveFixtures() =>
        TemplateFixtureLoader.LoadDirectory(Path.Combine(FixtureRoot, "positive"))
            .Select(fixture => new object[] { fixture });

    private static IEnumerable<object[]> GetNegativeFixtures() =>
        TemplateFixtureLoader.LoadDirectory(Path.Combine(FixtureRoot, "negative"))
            .Select(fixture => new object[] { fixture });

    [TestCaseSource(nameof(GetPositiveFixtures))]
    public void Render_Object_PositiveFixtureValidatesAndMatchesExactly(TemplateFixture fixture)
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        object model = fixture.Values;

        var validation = service.Validate(fixture.Template!);
        var result = service.Render(fixture.Template!, model, fixture.Format);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Diagnostics, Is.Empty);
            Assert.That(result, Is.EqualTo(fixture.Expected));
        }
    }

    [TestCaseSource(nameof(GetNegativeFixtures))]
    public void Render_Object_NegativeFixtureReportsStableCategory(TemplateFixture fixture)
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        object model = fixture.Values;

        var validation = service.Validate(fixture.Template!);
        var exception = Assert.Throws<TemplateRenderException>(
            () => service.Render(fixture.Template!, model, fixture.Format));

        Assert.That(
            exception!.Category,
            Is.EqualTo(fixture.ExpectedError),
            () => string.Join(Environment.NewLine, exception.Diagnostics.Select(value => value.Message)));

        if (fixture.ExpectedError == TemplateError.InvalidTemplate)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.Diagnostics, Has.Count.GreaterThanOrEqualTo(1));
                Assert.That(validation.Diagnostics[0].Line, Is.GreaterThan(0));
                Assert.That(validation.Diagnostics[0].Column, Is.GreaterThan(0));
            }
        }
        else
        {
            Assert.That(validation.IsValid, Is.True);
        }
    }

    [Test]
    public void Render_Object_NullArgumentsThrowArgumentNullException()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        object model = new { };

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<ArgumentNullException>(() => service.Render(null!, model, RenderAs.Text));
            Assert.Throws<ArgumentNullException>(() => service.Render("literal", (object)null!, RenderAs.Text));
        }
    }

    [Test]
    public void Render_Object_RepeatedCallsDoNotLeakValues()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(service.Render("{{ value }}", new { Value = "first" }, RenderAs.Text), Is.EqualTo("first"));
            Assert.That(service.Render("{{ value }}", new { Value = "second" }, RenderAs.Text), Is.EqualTo("second"));
        }

        var exception = Assert.Throws<TemplateRenderException>(
            () => service.Render("{{ value }}", new { }, RenderAs.Text));
        Assert.That(exception!.Category, Is.EqualTo(TemplateError.MissingValue));
    }

    [Test]
    public async Task Render_Object_ConcurrentCallsKeepContextsIsolated()
    {
        await using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var tasks = Enumerable.Range(0, 100)
            .Select(value => Task.Run(() => service.Render("{{ value }}", new { Value = value }, RenderAs.Text)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.That(results, Is.EqualTo(Enumerable.Range(0, 100).Select(value => value.ToString(CultureInfo.InvariantCulture))));
    }

    [Test]
    [NonParallelizable]
    public void Render_Object_UsesInvariantCultureUnderFrenchCulture()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var result = service.Render("{{ value }}", new { Value = 1234.5m }, RenderAs.Text);

            Assert.That(result, Is.EqualTo("1234.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Test]
    public void Render_Object_ExtractsPositiveAndNegativeReadingsFromSerializedJson()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var json = JsonSerializer.Serialize(new
        {
            readings = new[]
            {
                new { name = "supply", value = 21.75 },
                new { name = "return", value = -4.25 }
            }
        });

        var model = new
        {
            Payload = JsonSerializer.Deserialize<ReadingsPayload>(
                json,
                propertyNameCaseInsensitiveSerializerOptions)!
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(service.Render("{{ payload.readings[0].value }}", model, RenderAs.Text), Is.EqualTo("21.75"));
            Assert.That(service.Render("{{ payload.readings[1].value }}", model, RenderAs.Text), Is.EqualTo("-4.25"));
        }
    }

    [Test]
    public void Render_Object_UsesAllValuesDeserializedFromJsonSerializer()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        var options = new JsonSerializerOptions
        {
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(new
        {
            integer = 42,
            doubleValue = -21.75d,
            boolean = true,
            text = "supply",
            floatValue = 4.25f,
            longValue = long.MaxValue,
            mode = OperatingMode.Automatic
        }, options);

        object model = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, options)!;

        var renderedJson = service.Render(
            """
            {
              "Integer": {{ integer }},
              "DoubleValue": {{ doubleValue }},
              "Boolean": {{ boolean }},
              "Text": {{ text }},
              "FloatValue": {{ floatValue }},
              "LongValue": {{ longValue }},
              "Mode": {{ mode }}
            }
            """,
            model,
            RenderAs.Json);

        var renderedValues = JsonSerializer.Deserialize<RenderedValues>(renderedJson, options)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(renderedValues.Integer, Is.EqualTo(42));
            Assert.That(renderedValues.DoubleValue, Is.EqualTo(-21.75d));
            Assert.That(renderedValues.Boolean, Is.True);
            Assert.That(renderedValues.Text, Is.EqualTo("supply"));
            Assert.That(renderedValues.FloatValue, Is.EqualTo(4.25f));
            Assert.That(renderedValues.LongValue, Is.EqualTo(long.MaxValue));
            Assert.That(renderedValues.Mode, Is.EqualTo(OperatingMode.Automatic));
        }
    }

    [Test]
    public void Render_Object_CreatesValidJsonPayloadWithMultipleValues()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var model = new
        {
            Temperature = -4.25m,
            Enabled = true,
            Source = "plant/room\"1"
        };

        var result = service.Render(
            "{\"temperature\":{{ temperature }},\"enabled\":{{ enabled }},\"source\":{{ source }}}",
            model,
            RenderAs.Json);
        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.GetProperty("temperature").GetDecimal(), Is.EqualTo(-4.25m));
            Assert.That(root.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("source").GetString(), Is.EqualTo("plant/room\"1"));
        }
    }

    [Test]
    public void Render_Object_UsesFormatDeserializedFromWriteObjectYaml()
    {
        const string yaml = """
            write_object:
              format: json
              template: |
                {
                  "temperature": {{ temperature }},
                  "enabled": {{ enabled }},
                  "source": {{ source }}
                }
            """;
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var document = deserializer.Deserialize<WriteObjectDocument>(yaml);
        var model = new
        {
            Temperature = -4.25m,
            Enabled = true,
            Source = "plant/room\"1"
        };
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        var result = service.Render(
            document.WriteObject.Template,
            model,
            document.WriteObject.Format);
        using var json = JsonDocument.Parse(result);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(document.WriteObject.Format, Is.EqualTo(RenderAs.Json));
            Assert.That(json.RootElement.GetProperty("temperature").GetDecimal(), Is.EqualTo(-4.25m));
            Assert.That(json.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(json.RootElement.GetProperty("source").GetString(), Is.EqualTo("plant/room\"1"));
        }
    }

    [Test]
    public void Render_Object_ExtractsMultipleMqttReadingsAndCreatesCombinedPayload()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var mqttPayload = JsonSerializer.Serialize(new { supply = 19.5, returnValue = -2.75 });
        var payloadModel = new { Mqtt = JsonNode.Parse(mqttPayload) };
        var supply = service.Render("{{ mqtt.supply }}", payloadModel, RenderAs.Text);
        var returnValue = service.Render("{{ mqtt[\"returnValue\"] }}", payloadModel, RenderAs.Text);
        var outputModel = new
        {
            Supply = decimal.Parse(supply, CultureInfo.InvariantCulture),
            Return = decimal.Parse(returnValue, CultureInfo.InvariantCulture)
        };

        var result = service.Render("supply={{ supply }};return={{ return }}", outputModel, RenderAs.Text);

        Assert.That(result, Is.EqualTo("supply=19.5;return=-2.75"));
    }
}