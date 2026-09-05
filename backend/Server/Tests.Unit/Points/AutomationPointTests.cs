using Server.Common.Contracts;
using Server.Common.Models;
using Server.Common.Types;
using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tests.Unit.Api;

namespace Tests.Unit.Points;

[TestFixture]
internal sealed class AutomationPointTests
{
    [TestCase(PointSourceType.Virtual)]
    [TestCase(PointSourceType.Physical)]
    [TestCase(PointSourceType.Remote)]
    public void JsonAndYamlRoundTripsPreserveConcreteTypeAndSharedProperties(PointSourceType sourceType)
    {
        var point = Point(sourceType) with
        {
            Description = "Temperature sensor",
            Units = "degC",
            Limits = new JsonObject { ["minimum"] = -20, ["maximum"] = 100 },
            Revision = 3,
            CreatedAt = "2026-09-05T00:00:00Z"
        };
        var json = JsonSerializer.Serialize(point, FlowControlJson.Options);
        var restored = JsonSerializer.Deserialize<AutomationPoint>(json, FlowControlJson.Options)!;
        var yamlPoint = PointYaml.Parse(Encoding.UTF8.GetBytes(PointYaml.Render(point)));

        Assert.Multiple(() =>
        {
            Assert.That(restored.GetType(), Is.EqualTo(point.GetType()));
            Assert.That(restored.PointSourceType, Is.EqualTo(sourceType));
            Assert.That(restored.Revision, Is.EqualTo(3));
            Assert.That(restored.CreatedAt, Is.EqualTo(point.CreatedAt));
            Assert.That(yamlPoint.GetType(), Is.EqualTo(point.GetType()));
            Assert.That(yamlPoint.Revision, Is.Zero);
            Assert.That(yamlPoint.CreatedAt, Is.Null);
            Assert.That(yamlPoint.Description, Is.EqualTo(point.Description));
            Assert.That(yamlPoint.Units, Is.EqualTo(point.Units));
            Assert.That(JsonNode.DeepEquals(yamlPoint.Limits, point.Limits), Is.True);
            Assert.That(JsonNode.DeepEquals(yamlPoint.Mapping, point.Mapping), Is.True);
        });
    }

    [TestCase(null)]
    [TestCase("unknown")]
    public void RejectsMissingOrInvalidSourceType(string? sourceType)
    {
        var json = JsonSerializer.SerializeToNode(Point(PointSourceType.Virtual), FlowControlJson.Options)!.AsObject();
        json.Remove("pointSourceType");
        if (sourceType is not null)
        {
            json["pointSourceType"] = sourceType;
        }

        Assert.That(() => json.Deserialize<AutomationPoint>(FlowControlJson.Options), Throws.TypeOf<JsonException>());
    }

    [TestCase(PointSourceType.Virtual)]
    [TestCase(PointSourceType.Physical)]
    [TestCase(PointSourceType.Remote)]
    public void ValidationUsesConcreteTypeAndRejectsInvalidDirection(PointSourceType sourceType)
    {
        var validator = new PointDefinitionValidator();
        var context = new PointValidationContext(
            new Dictionary<string, PointGroup>(),
            new Dictionary<string, PointSource> { ["mqtt"] = Source() });
        var point = Point(sourceType);

        var validated = validator.Validate(point, context);
        var invalid = point with
        {
            Direction = sourceType == PointSourceType.Virtual
                ? DataDirectionType.Input : DataDirectionType.Value
        };

        Assert.Multiple(() =>
        {
            Assert.That(validated.PointSourceType, Is.EqualTo(sourceType));
            Assert.That(validated.Source, Is.SameAs(point));
            Assert.That(() => validator.Validate(invalid, context),
                Throws.TypeOf<PointDefinitionValidationException>());
        });
    }

    [Test]
    public void PhysicalOutputRequiresSafetyPolicy()
    {
        var validator = new PointDefinitionValidator();
        var context = new PointValidationContext(
            new Dictionary<string, PointGroup>(), new Dictionary<string, PointSource>());
        var output = Point(PointSourceType.Physical) with
        {
            Direction = DataDirectionType.Output,
            Commandable = true
        };

        Assert.That(() => validator.Validate(output, context),
            Throws.TypeOf<PointDefinitionValidationException>());

        output = output with
        {
            SafeDisablePolicy = new JsonObject
            {
                ["startup"] = "stop_driving",
                ["shutdown"] = "stop_driving",
                ["communicationLoss"] = "stop_driving",
                ["disable"] = "stop_driving"
            }
        };
        Assert.That(validator.Validate(output, context).SafetyPolicy, Is.Not.Null);
    }

    [Test]
    public async Task MixedPointCataloguePreservesTypesThroughCreateUpdateAndList()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var sources = scope.ServiceProvider.GetRequiredService<IPointSourceService>();
        await sources.CreateAsync(Source(), default);
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        AutomationPoint[] points =
        [
            Point(PointSourceType.Virtual),
            Point(PointSourceType.Physical),
            Point(PointSourceType.Remote)
        ];

        foreach (var point in points)
        {
            var created = await store.CreatePointAsync(point, default);
            var loaded = await store.GetPointAsync(point.Id, default);
            var updated = await store.UpdatePointAsync(point.Id,
                loaded with { Description = "Updated" }, loaded.Revision, default);
            Assert.Multiple(() =>
            {
                Assert.That(created.GetType(), Is.EqualTo(point.GetType()));
                Assert.That(loaded.GetType(), Is.EqualTo(point.GetType()));
                Assert.That(updated.GetType(), Is.EqualTo(point.GetType()));
                Assert.That(updated.Revision, Is.EqualTo(2));
            });
        }

        var listed = await store.ListPointsAsync(default);
        Assert.That(listed.Select(point => point.GetType()),
            Is.EquivalentTo(points.Select(point => point.GetType())));

        using var response = await client.GetAsync("/api/points");
        response.EnsureSuccessStatusCode();
        var page = JsonSerializer.Deserialize<PaginatedResult<AutomationPoint>>(
            await response.Content.ReadAsStringAsync(), FlowControlJson.Options)!;
        Assert.That(page.Items.Select(point => point.GetType()),
            Is.EquivalentTo(points.Select(point => point.GetType())));
    }

    private static AutomationPoint Point(PointSourceType sourceType) => sourceType switch
    {
        PointSourceType.Virtual => new VirtualAutomationPoint
        {
            Id = "virtual-point",
            Name = "Virtual",
            Enabled = true,
            Direction = DataDirectionType.Value,
            ValueType = AutomationPointValueType.Analog,
            Readable = true,
            Persistence = "volatile"
        },
        PointSourceType.Physical => new PhysicalAutomationPoint
        {
            Id = "physical-point",
            Name = "Physical",
            Enabled = true,
            Direction = DataDirectionType.Input,
            ValueType = AutomationPointValueType.Analog,
            Readable = true,
            Persistence = "volatile"
        },
        PointSourceType.Remote => new RemoteAutomationPoint
        {
            Id = "remote-point",
            Name = "Remote",
            Enabled = true,
            Direction = DataDirectionType.Input,
            ValueType = AutomationPointValueType.Analog,
            Readable = true,
            Persistence = "volatile",
            SourceId = "mqtt",
            Mapping = new JsonObject { ["stateTopic"] = "sensor/temperature" }
        },
        _ => throw new ArgumentOutOfRangeException(nameof(sourceType))
    };

    private static PointSource Source() => new()
    {
        Id = "mqtt",
        Name = "MQTT",
        Enabled = true,
        Kind = "mqtt",
        Connection = new PointSourceConnection { BrokerUrl = "mqtt://localhost:1883", Qos = 0 },
        Timeouts = new PointSourceTimeouts { ConnectMilliseconds = 1000 }
    };
}