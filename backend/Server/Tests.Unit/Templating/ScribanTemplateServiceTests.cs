using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server.Common.Contracts.Templating;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Assert = NUnit.Framework.Assert;
using TestContext = NUnit.Framework.TestContext;

namespace Tests.Unit.Templating;

[TestClass]
public sealed class ScribanTemplateServiceTests
{
    private static string FixtureRoot => Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "TemplateFixtures");

    public static IEnumerable<object[]> GetPositiveFixtures() =>
        TemplateFixtureLoader.LoadDirectory(Path.Combine(FixtureRoot, "positive"))
            .Select(fixture => new object[] { fixture });

    public static IEnumerable<object[]> GetNegativeFixtures() =>
        TemplateFixtureLoader.LoadDirectory(Path.Combine(FixtureRoot, "negative"))
            .Select(fixture => new object[] { fixture });

    [TestMethod]
    [DynamicData(nameof(GetPositiveFixtures))]
    public void Render_PositiveFixtureValidatesAndMatchesExactly(TemplateFixture fixture)
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        var validation = service.Validate(fixture.Template!);
        var result = service.Render(fixture.Template!, fixture.Values);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Diagnostics, Is.Empty);
            Assert.That(result, Is.EqualTo(fixture.Expected));
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetNegativeFixtures))]
    public void Render_NegativeFixtureReportsStableCategory(TemplateFixture fixture)
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        var validation = service.Validate(fixture.Template!);
        var exception = Assert.Throws<TemplateRenderException>(
            () => service.Render(fixture.Template!, fixture.Values));

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

    [TestMethod]
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

    [TestMethod]
    public void Validate_NullTemplateThrowsArgumentNullException()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        Assert.Throws<ArgumentNullException>(() => service.Validate(null!));
    }

    [TestMethod]
    public void Render_NullArgumentsThrowArgumentNullException()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<ArgumentNullException>(() => service.Render(null!, new Dictionary<string, object?>()));
            Assert.Throws<ArgumentNullException>(() => service.Render("literal", null!));
        }
    }

    [TestMethod]
    public void Render_RepeatedCallsDoNotLeakValues()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                    service.Render("{{ value }}", new Dictionary<string, object?> { ["value"] = "first" }),
                    Is.EqualTo("first"));
            Assert.That(
                service.Render("{{ value }}", new Dictionary<string, object?> { ["value"] = "second" }),
                Is.EqualTo("second"));
        }

        var exception = Assert.Throws<TemplateRenderException>(
            () => service.Render("{{ value }}", new Dictionary<string, object?>()));
        Assert.That(exception!.Category, Is.EqualTo(TemplateError.MissingValue));
    }

    [TestMethod]
    public async Task Render_ConcurrentCallsKeepContextsIsolated()
    {
        await using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var tasks = Enumerable.Range(0, 100)
            .Select(value => Task.Run(() => service.Render(
                "{{ value }}",
                new Dictionary<string, object?> { ["value"] = value })))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.That(results, Is.EqualTo(Enumerable.Range(0, 100).Select(value => value.ToString(CultureInfo.InvariantCulture))));
    }

    [TestMethod]
    [DoNotParallelize]
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
                new Dictionary<string, object?> { ["value"] = 1234.5m });

            Assert.That(result, Is.EqualTo("1234.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
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
            Assert.That(service.Render("{{ payload.readings[0].value }}", values), Is.EqualTo("21.75"));
            Assert.That(service.Render("{{ payload.readings[1].value }}", values), Is.EqualTo("-4.25"));
        }
    }

    [TestMethod]
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
            "{\"temperature\":{{ temperature | json }},\"enabled\":{{ enabled | json }},\"source\":{{ source | json }}}",
            values);
        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.GetProperty("temperature").GetDecimal(), Is.EqualTo(-4.25m));
            Assert.That(root.GetProperty("enabled").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("source").GetString(), Is.EqualTo("plant/room\"1"));
        }
    }

    [TestMethod]
    public void Render_ExtractsMultipleMqttReadingsAndCreatesCombinedPayload()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var mqttPayload = JsonSerializer.Serialize(new { supply = 19.5, returnValue = -2.75 });
        var payloadValues = new Dictionary<string, object?> { ["mqtt"] = JsonNode.Parse(mqttPayload) };
        var supply = service.Render("{{ mqtt.supply }}", payloadValues);
        var returnValue = service.Render("{{ mqtt[\"returnValue\"] }}", payloadValues);
        var outputValues = new Dictionary<string, object?>
        {
            ["supply"] = decimal.Parse(supply, CultureInfo.InvariantCulture),
            ["return"] = decimal.Parse(returnValue, CultureInfo.InvariantCulture)
        };

        var result = service.Render(
            "supply={{ supply }};return={{ return }}",
            outputValues);

        Assert.That(result, Is.EqualTo("supply=19.5;return=-2.75"));
    }
}