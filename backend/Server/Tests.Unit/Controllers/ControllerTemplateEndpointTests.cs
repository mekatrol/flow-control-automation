using Server.Api.Contracts;
using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Tests.Unit.Controllers;

[TestFixture]
internal sealed class ControllerTemplateEndpointTests
{
    [Test]
    public async Task DefaultIsAlwaysAvailableAndReadOnly()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        var template = await client.GetFromJsonAsync<ControllerTemplate>(
            "/api/controller-templates/default",
            FlowControlJson.Options);
        using var yaml = await client.GetAsync("/api/controller-templates/default/yaml");
        using var delete = await client.DeleteAsync(
            "/api/controller-templates/default?revision=1");

        Assert.Multiple(() =>
        {
            Assert.That(template?.Id, Is.EqualTo("default"));
            Assert.That(template?.ReadOnly, Is.True);
            Assert.That(yaml.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(yaml.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/yaml"));
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        });
    }

    [Test]
    public async Task CustomTemplateRoundTripsWithRevisionAndReopens()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var input = Template();

        using var create = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/controller-templates",
            input);
        var created = await create.Content.ReadFromJsonAsync<ControllerTemplate>(
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(created?.Revision, Is.EqualTo(1));
            Assert.That(created?.CreatedAt, Is.Not.Null);
            Assert.That(File.Exists(factory.ControllerDataPath), Is.True);
        });

        using var update = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/controller-templates/compact",
            input with { Name = "Compact updated" },
            revision: 1);
        var updated = await update.Content.ReadFromJsonAsync<ControllerTemplate>(
            FlowControlJson.Options);
        Assert.That(updated?.Revision, Is.EqualTo(2));

        using var stale = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/controller-templates/compact",
            input,
            revision: 1);
        Assert.That(stale.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        using var secondClient = factory.CreateClient();
        var reopened = await secondClient.GetFromJsonAsync<ControllerTemplate>(
            "/api/controller-templates/compact",
            FlowControlJson.Options);
        Assert.That(reopened?.Name, Is.EqualTo("Compact updated"));

        using var delete = await secondClient.DeleteAsync(
            "/api/controller-templates/compact?revision=2");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task ValidationReportsSemanticPathsAndSyntaxLocations()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        using var semantic = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/controller-templates/validate",
            Template() with
            {
                Capabilities = Template().Capabilities with
                {
                    PointTypes = ["not-a-type"],
                },
            });
        var semanticBody = await semantic.Content.ReadAsStringAsync();

        using var syntax = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/controller-templates/validate")
        {
            Content = new StringContent(
                $"schemaVersion: 1{Environment.NewLine}id: [broken{Environment.NewLine}",
                Encoding.UTF8,
                "application/yaml")
        };
        using var syntaxResponse = await client.SendAsync(syntax);
        var syntaxError = await syntaxResponse.Content.ReadFromJsonAsync<DefinitionErrorResponse>(
            FlowControlJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(semantic.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(semanticBody, Does.Contain("capabilities.pointTypes[0]"));
            Assert.That(syntaxResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(syntaxError?.Code, Is.EqualTo("yaml_syntax"));
            Assert.That(syntaxError?.Details?.ToString(), Does.Contain("line"));
        });
    }

    [Test]
    public async Task ListIsDeterministicAndIncludesDefault()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        foreach (var template in new[]
        {
            Template() with { Id = "zulu", Name = "Zulu" },
            Template() with { Id = "alpha", Name = "Alpha" }
        })
        {
            using var response = await SendYaml(
                client,
                HttpMethod.Post,
                "/api/controller-templates",
                template);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        var json = await client.GetFromJsonAsync<TemplateList>(
            "/api/controller-templates",
            FlowControlJson.Options);
        Assert.That(
            json?.Items.Select(item => item.Id),
            Is.EqualTo(new[] { "default", "alpha", "zulu" }));
    }

    private static async Task<HttpResponseMessage> SendYaml(
        HttpClient client,
        HttpMethod method,
        string path,
        ControllerTemplate template,
        int? revision = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(
                ControllerTemplateYaml.Render(template),
                Encoding.UTF8,
                "application/yaml")
        };
        if (revision is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", revision.Value.ToString());
        }

        return await client.SendAsync(request);
    }

    private static ControllerTemplate Template() => new()
    {
        Id = "compact",
        Name = "Compact",
        Capabilities = new()
        {
            PointTypes = ["digital"],
            PointDirections = ["input", "output"],
            PointFeatures = ["read", "command"],
            ConnectorDataTypes = ["boolean"],
            FlowFunctions = ["and", "read-point", "write-point"],
            ExecutionModes = ["interval"],
            RuntimeFeatures = ["bound_points"]
        },
        Limits = new()
        {
            MaxFlows = 8,
            MaxNodesPerFlow = 64,
            MaxConnectionsPerFlow = 96,
            MinimumIntervalMilliseconds = 100
        }
    };

    private sealed record TemplateList(IReadOnlyList<ControllerTemplate> Items);
}