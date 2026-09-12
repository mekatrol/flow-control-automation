using Server.Common.Contracts.Templating;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tests.Unit.Templating;

[TestFixture]
public sealed class TemplateServiceDictionaryTests
{
    private static readonly JsonSerializerOptions jsonStringEnumConverterSerializerOptions = new()
    {
        UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
        Converters = { new JsonStringEnumConverter() }
    };

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
        OperatingMode Mode);

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
    public void Render_PositiveFixtureValidatesAndMatchesExactly(TemplateFixture fixture)
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        var validation = service.Validate(fixture.Template!);
        var result = service.Render(fixture.Template!, fixture.Values, fixture.Format);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Diagnostics, Is.Empty);
            Assert.That(result, Is.EqualTo(fixture.Expected));
        }
    }

    [TestCaseSource(nameof(GetNegativeFixtures))]
    public void Render_NegativeFixtureReportsStableCategory(TemplateFixture fixture)
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        var validation = service.Validate(fixture.Template!);
        var exception = Assert.Throws<TemplateRenderException>(
            () => service.Render(fixture.Template!, fixture.Values, fixture.Format));

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
    public void AddServerServices_RegistersExactlyOneSingletonTemplateService()
    {
        using var provider = Helpers.TestServices.CreateProvider();

        var services = provider.GetServices<ITemplateService>().ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(services, Has.Length.EqualTo(1));
            Assert.That(provider.GetRequiredService<ITemplateService>(), Is.SameAs(services[0]));
        }
    }

    [Test]
    public void Validate_NullTemplateThrowsArgumentNullException()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        Assert.Throws<ArgumentNullException>(() => service.Validate(null!));
    }

    [Test]
    public void Render_NullArgumentsThrowArgumentNullException()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<ArgumentNullException>(() => service.Render(null!, new Dictionary<string, object?>(), RenderAs.Text));
            Assert.Throws<ArgumentNullException>(() => service.Render("literal", null!, RenderAs.Text));
        }
    }

    [Test]
    public void Render_RepeatedCallsDoNotLeakValues()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                    service.Render("{{ value }}", new Dictionary<string, object?> { ["value"] = "first" }, RenderAs.Text),
                    Is.EqualTo("first"));
            Assert.That(
                service.Render("{{ value }}", new Dictionary<string, object?> { ["value"] = "second" }, RenderAs.Text),
                Is.EqualTo("second"));
        }

        var exception = Assert.Throws<TemplateRenderException>(
            () => service.Render("{{ value }}", new Dictionary<string, object?>(), RenderAs.Text));
        Assert.That(exception!.Category, Is.EqualTo(TemplateError.MissingValue));
    }

    [Test]
    public void Render_ObjectModelExposesMembersToTemplateEngine()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var model = new
        {
            DeviceName = "supply",
            Reading = new { Value = -4.25m }
        };

        var result = service.Render(
            "{{ device_name }}={{ reading.value }}",
            model,
            RenderAs.Text);

        Assert.That(result, Is.EqualTo("supply=-4.25"));
    }

    [Test]
    public async Task Render_ConcurrentCallsKeepContextsIsolated()
    {
        await using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var tasks = Enumerable.Range(0, 100)
            .Select(value => Task.Run(() => service.Render(
                "{{ value }}",
                new Dictionary<string, object?> { ["value"] = value },
                RenderAs.Text)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.That(results, Is.EqualTo(Enumerable.Range(0, 100).Select(value => value.ToString(CultureInfo.InvariantCulture))));
    }

    [Test]
    [NonParallelizable]
    public void Render_UsesInvariantCultureUnderFrenchCulture()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var result = service.Render(
                "{{ value }}",
                new Dictionary<string, object?> { ["value"] = 1234.5m },
                RenderAs.Text);

            Assert.That(result, Is.EqualTo("1234.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Test]
    public void Render_ExtractsPositiveAndNegativeReadingsFromSerializedJson()
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

        var values = new Dictionary<string, object?> { ["payload"] = JsonNode.Parse(json) };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(service.Render("{{ payload.readings[0].value }}", values, RenderAs.Text), Is.EqualTo("21.75"));
            Assert.That(service.Render("{{ payload.readings[1].value }}", values, RenderAs.Text), Is.EqualTo("-4.25"));
        }
    }

    [Test]
    public void Render_UsesAllValuesDeserializedFromJsonSerializer()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        var json = JsonSerializer.Serialize(new
        {
            integer = 42,
            doubleValue = -21.75d,
            boolean = true,
            text = "supply",
            floatValue = 4.25f,
            mode = OperatingMode.Automatic
        }, jsonStringEnumConverterSerializerOptions);

        var values = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, jsonStringEnumConverterSerializerOptions)!;

        var renderedJson = service.Render(
            """
            {
              "Integer": {{ integer }},
              "DoubleValue": {{ doubleValue }},
              "Boolean": {{ boolean }},
              "Text": {{ text }},
              "FloatValue": {{ floatValue }},
              "Mode": {{ mode }}
            }
            """,
            values,
            RenderAs.Json);

        var renderedValues = JsonSerializer.Deserialize<RenderedValues>(renderedJson, jsonStringEnumConverterSerializerOptions)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(renderedValues.Integer, Is.EqualTo(42));
            Assert.That(renderedValues.DoubleValue, Is.EqualTo(-21.75d));
            Assert.That(renderedValues.Boolean, Is.True);
            Assert.That(renderedValues.Text, Is.EqualTo("supply"));
            Assert.That(renderedValues.FloatValue, Is.EqualTo(4.25f));
            Assert.That(renderedValues.Mode, Is.EqualTo(OperatingMode.Automatic));
        }
    }

    [Test]
    public void Render_CreatesValidJsonPayloadWithMultipleValues()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var values = new Dictionary<string, object?>
        {
            ["temperature"] = -4.25m,
            ["enabled"] = true,
            ["source"] = "plant/room\"1"
        };

        var result = service.Render(
            "{\"temperature\":{{ temperature }},\"enabled\":{{ enabled }},\"source\":{{ source }}}",
            values,
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
    public void Render_JsonRejectsAnInvalidCompletedDocument()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        var exception = Assert.Throws<TemplateRenderException>(() => service.Render(
            "{\"value\": {{ value }}",
            new Dictionary<string, object?> { ["value"] = "payload" },
            RenderAs.Json));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Category, Is.EqualTo(TemplateError.RenderFailed));
            Assert.That(exception.Message, Does.Contain("valid JSON document"));
        }
    }

    [Test]
    public void Render_ExtractsMultipleMqttReadingsAndCreatesCombinedPayload()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var mqttPayload = JsonSerializer.Serialize(new { supply = 19.5, returnValue = -2.75 });
        var payloadValues = new Dictionary<string, object?> { ["mqtt"] = JsonNode.Parse(mqttPayload) };
        var supply = service.Render("{{ mqtt.supply }}", payloadValues, RenderAs.Text);
        var returnValue = service.Render("{{ mqtt[\"returnValue\"] }}", payloadValues, RenderAs.Text);

        var outputValues = new Dictionary<string, object?>
        {
            ["supply"] = decimal.Parse(supply, CultureInfo.InvariantCulture),
            ["return"] = decimal.Parse(returnValue, CultureInfo.InvariantCulture)
        };

        var result = service.Render(
            "supply={{ supply }};return={{ return }}",
            outputValues,
            RenderAs.Text);

        Assert.That(result, Is.EqualTo("supply=19.5;return=-2.75"));
    }
}