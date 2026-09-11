using Server.Api.ConfigurationGuidance;
using Server.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Tests.Unit.Configuration;

[TestFixture]
public sealed class ConfigurationGuidanceTests
{
    [Test]
    public void EveryConfigurationFieldHasExactlyOneCurrentHelpEntry()
    {
        Assert.That(ConfigurationGuidance.CoverageErrors(), Is.Empty);
    }

    [Test]
    public void GuidanceIsMarkdownGeneratedForCurrentPointValues()
    {
        var yaml = "schemaVersion: 1\npoints:\n  - pointSourceType: remote\n    direction: input\n    valueType: digital\n    commandable: false\n"u8;
        var markdown = ConfigurationGuidance.Render("point", yaml);
        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.StartWith("# Digital Input Remote point YAML guidance"));
            Assert.That(markdown, Does.Contain("| `pointSourceType` |"));
            Assert.That(markdown, Does.Contain("| `stateLabels` |"));
            Assert.That(markdown, Does.Contain("| `mapping` |"));
            Assert.That(markdown, Does.Not.Contain("| `units` |"));
            Assert.That(markdown, Does.Not.Contain("| `relinquishDefault` |"));
            Assert.That(markdown, Does.Contain("```yaml"));
        });
    }

    [Test]
    public void PointSourceGuidanceDescribesPrivateNetworkAccessForEverySourceKind()
    {
        var yaml = """
            schemaVersion: 1
            sources:
              - id: weather
                name: Weather API
                enabled: true
                kind: httpJson
                connection:
                  baseUrl: https://api.example.com
                  allowPrivateNetwork: false
                  maximumResponseBytes: 65536
                tls:
                  verifyServerCertificate: true
                timeouts:
                  connectMilliseconds: 2000
                  requestMilliseconds: 5000
            """u8;
        var markdown = ConfigurationGuidance.Render("point-source", yaml);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("| `allowPrivateNetwork` |"));
            Assert.That(markdown, Does.Contain("Explicitly permit private-network destinations."));
            Assert.That(markdown, Does.Not.Contain("private-network MQTT destinations"));
        });
    }

    [Test]
    public async Task EndpointReturnsCorrectGuidanceForEveryPointCombination()
    {
        await using var factory = new Api.FlowControlApplicationFactory(services =>
        {
            services.RemoveAll<IPointSourceService>();
            services.AddSingleton<IPointSourceService, GuidancePointSourceService>();
        });
        using var client = factory.CreateClient();
        var bindings = new[]
        {
            (Type: PointSourceType.Virtual, SourceId: null, Kind: null),
            (Type: PointSourceType.Physical, SourceId: null, Kind: null),
            (Type: PointSourceType.Remote, SourceId: "home-assistant", Kind: "homeAssistant"),
            (Type: PointSourceType.Remote, SourceId: "mqtt", Kind: "mqtt"),
            (Type: PointSourceType.Remote, SourceId: "http-json", Kind: "httpJson")
        };
        var combinations = (
            from binding in bindings
            from direction in Enum.GetValues<DataDirectionType>()
            from valueType in Enum.GetValues<AutomationPointValueType>()
            from commandable in new[] { false, true }
            select (binding, direction, valueType, commandable)).ToArray();

        foreach (var (binding, direction, valueType, commandable) in combinations)
        {
            var source = WireValue(binding.Type);
            var dataDirection = WireValue(direction);
            var value = WireValue(valueType);
            var sourceReference = binding.SourceId is null ? string.Empty : $"    sourceId: {binding.SourceId}\n";
            var yaml = $"""
                schemaVersion: 1
                points:
                  - id: guidance-test
                    name: Guidance test
                    enabled: true
                    pointSourceType: {source}
                    direction: {dataDirection}
                    valueType: {value}
                    readable: true
                    commandable: {commandable.ToString().ToLowerInvariant()}
                    persistence: volatile
                {sourceReference}    mapping:
                      channel: DI-1
                """;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/configuration-guidance/point")
            {
                Content = new StringContent(yaml, Encoding.UTF8, "application/yaml")
            };
            using var response = await client.SendAsync(request);
            var markdown = await response.Content.ReadAsStringAsync();
            var description = $"{source}/{binding.Kind}/{dataDirection}/{value}/commandable={commandable}";

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), description);
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/markdown"), description);
                Assert.That(markdown, Does.StartWith($"# {Title(value)} {Title(dataDirection)} {Title(source)} point YAML guidance"), description);
                Assert.That(HasField(markdown, "units"), Is.EqualTo(valueType is AutomationPointValueType.Analog or AutomationPointValueType.Integer), description);
                Assert.That(HasField(markdown, "stateLabels"), Is.EqualTo(valueType is AutomationPointValueType.Digital or AutomationPointValueType.MultiState), description);
                Assert.That(HasField(markdown, "limits"), Is.EqualTo(valueType is AutomationPointValueType.Analog or AutomationPointValueType.Integer or AutomationPointValueType.Text), description);
                Assert.That(HasField(markdown, "relinquishDefault"), Is.EqualTo(binding.Type == PointSourceType.Virtual), description);
                Assert.That(HasField(markdown, "sourceId"), Is.EqualTo(binding.Type == PointSourceType.Remote), description);
                Assert.That(HasField(markdown, "mapping"), Is.EqualTo(binding.Type == PointSourceType.Remote), description);
                Assert.That(HasField(markdown, "safeDisablePolicy"), Is.EqualTo(binding.Type != PointSourceType.Virtual && commandable), description);
                Assert.That(HasField(markdown, "entityId"), Is.EqualTo(binding.Kind == "homeAssistant"), description);
                Assert.That(HasField(markdown, "stateTopic"), Is.EqualTo(binding.Kind == "mqtt"), description);
                Assert.That(HasField(markdown, "path"), Is.EqualTo(binding.Kind == "httpJson"), description);
                Assert.That(markdown, Does.Contain("```yaml"), description);
                Assert.That(markdown, Does.Contain($"pointSourceType: {source}"), description);
                Assert.That(markdown, Does.Contain($"direction: {dataDirection}"), description);
                Assert.That(markdown, Does.Contain($"valueType: {value}"), description);

                if (binding.Kind is not null)
                {
                    Assert.That(markdown, Does.Contain($"## {Title(binding.Kind)} mapping"), description);
                    Assert.That(markdown, Does.Not.Contain("channel: DI-1"), description);
                    Assert.That(markdown, Does.Contain(binding.Kind switch
                    {
                        "homeAssistant" => "entityId: binary_sensor.example",
                        "mqtt" => "stateTopic: devices/example/state",
                        "httpJson" => "path: /points/example",
                        _ => throw new InvalidOperationException()
                    }), description);
                }
            });
        }

        Assert.That(combinations, Has.Length.EqualTo(
            bindings.Length
            * Enum.GetValues<DataDirectionType>().Length
            * Enum.GetValues<AutomationPointValueType>().Length
            * 2));
    }

    private static bool HasField(string markdown, string field) =>
        markdown.Contains($"| `{field}` |", StringComparison.Ordinal);

    private static string WireValue<T>(T value) where T : struct, Enum =>
        JsonSerializer.Serialize(value, FlowControlJson.Options).Trim('"');

    private static string Title(string value) =>
        char.ToUpperInvariant(value[0]) + value[1..];

    private sealed class GuidancePointSourceService : IPointSourceService
    {
        public Task<PointSource> GetAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new PointSource
            {
                Id = id,
                Name = id,
                Enabled = true,
                Kind = id switch
                {
                    "home-assistant" => "homeAssistant",
                    "mqtt" => "mqtt",
                    "http-json" => "httpJson",
                    _ => throw new PointSourceNotFoundException(id)
                }
            });

        public Task<PaginatedResult<PointSource>> ListAsync(PointSourceListOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(new PaginatedResult<PointSource>([], 0, 1, options.PageSize, 0));

        public Task<PointSource> CreateAsync(PointSource source, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PointSource> UpdateAsync(string id, PointSource source, int revision, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string id, int revision, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    [Test]
    public async Task EndpointDoesNotDeserializeTransientPointValues()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        const string yaml = """
            schemaVersion: 1
            points:
              - pointSourceType: 1
                direction: input
                valueType: digital
                commandable: false
                mapping: unfinished
            """;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/configuration-guidance/point")
        {
            Content = new StringContent(yaml, Encoding.UTF8, "application/yaml")
        };

        using var response = await client.SendAsync(request);
        var markdown = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(markdown, Does.StartWith("# Digital Input Point point YAML guidance"));
            Assert.That(markdown, Does.Contain("mapping: unfinished"));
        });
    }
}
