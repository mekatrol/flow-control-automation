using Server.Api.Contracts;
using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Tests.Unit.Controllers;

[TestFixture]
internal sealed class ControllerTemplateEndpointTests
{

    /// <summary>
    /// Purpose: Protects the behavioral contract that default is always available and read only.
    /// Description: Arranges the inputs for default is always available and read only, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // default is always available and read only.
        Assert.Multiple(() =>
        {

            // Expected outcome: `template?.Id` has the required value.
            // Acceptance criteria: `template?.Id` must equal `"default"`, because this condition proves that
            // default is always available and read only.
            Assert.That(template?.Id, Is.EqualTo("default"));

            // Expected outcome: `template?.ReadOnly` confirms the required condition.
            // Acceptance criteria: `template?.ReadOnly` must be true, because this condition proves that
            // default is always available and read only.
            Assert.That(template?.ReadOnly, Is.True);

            // Expected outcome: `yaml.StatusCode` has the required value.
            // Acceptance criteria: `yaml.StatusCode` must equal `HttpStatusCode.OK`, because this condition proves that
            // default is always available and read only.
            Assert.That(yaml.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Expected outcome: `yaml.Content.Headers.ContentType?.MediaType` has the required value.
            // Acceptance criteria: `yaml.Content.Headers.ContentType?.MediaType` must equal `"application/yaml"`, because this condition proves that
            // default is always available and read only.
            Assert.That(yaml.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/yaml"));

            // Expected outcome: `delete.StatusCode` has the required value.
            // Acceptance criteria: `delete.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
            // default is always available and read only.
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that custom template round trips with revision and reopens.
    /// Description: Arranges the inputs for custom template round trips with revision and reopens, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // custom template round trips with revision and reopens.
        Assert.Multiple(() =>
        {

            // Expected outcome: `create.StatusCode` has the required value.
            // Acceptance criteria: `create.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
            // custom template round trips with revision and reopens.
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created));

            // Expected outcome: `created?.Revision` has the required value.
            // Acceptance criteria: `created?.Revision` must equal `1`, because this condition proves that
            // custom template round trips with revision and reopens.
            Assert.That(created?.Revision, Is.EqualTo(1));

            // Expected outcome: `created?.CreatedAt` is available.
            // Acceptance criteria: `created?.CreatedAt` must not be null, because this condition proves that
            // custom template round trips with revision and reopens.
            Assert.That(created?.CreatedAt, Is.Not.Null);

            // Expected outcome: `File.Exists(factory.ControllerDataPath` confirms the required condition.
            // Acceptance criteria: `File.Exists(factory.ControllerDataPath` must be true, because this condition proves that
            // custom template round trips with revision and reopens.
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

        // Expected outcome: `updated?.Revision` has the required value.
        // Acceptance criteria: `updated?.Revision` must equal `2`, because this condition proves that
        // custom template round trips with revision and reopens.
        Assert.That(updated?.Revision, Is.EqualTo(2));

        using var stale = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/controller-templates/compact",
            input,
            revision: 1);

        // Expected outcome: `stale.StatusCode` has the required value.
        // Acceptance criteria: `stale.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
        // custom template round trips with revision and reopens.
        Assert.That(stale.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        using var secondClient = factory.CreateClient();
        var reopened = await secondClient.GetFromJsonAsync<ControllerTemplate>(
            "/api/controller-templates/compact",
            FlowControlJson.Options);

        // Expected outcome: `reopened?.Name` has the required value.
        // Acceptance criteria: `reopened?.Name` must equal `"Compact updated"`, because this condition proves that
        // custom template round trips with revision and reopens.
        Assert.That(reopened?.Name, Is.EqualTo("Compact updated"));

        using var delete = await secondClient.DeleteAsync(
            "/api/controller-templates/compact?revision=2");

        // Expected outcome: `delete.StatusCode` has the required value.
        // Acceptance criteria: `delete.StatusCode` must equal `HttpStatusCode.NoContent`, because this condition proves that
        // custom template round trips with revision and reopens.
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that validation reports semantic paths and syntax locations.
    /// Description: Arranges the inputs for validation reports semantic paths and syntax locations, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // validation reports semantic paths and syntax locations.
        Assert.Multiple(() =>
        {

            // Expected outcome: `semantic.StatusCode` has the required value.
            // Acceptance criteria: `semantic.StatusCode` must equal `HttpStatusCode.OK`, because this condition proves that
            // validation reports semantic paths and syntax locations.
            Assert.That(semantic.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Expected outcome: `semanticBody` includes the required content.
            // Acceptance criteria: `semanticBody` must contain `"capabilities.pointTypes[0]"`, because this condition proves that
            // validation reports semantic paths and syntax locations.
            Assert.That(semanticBody, Does.Contain("capabilities.pointTypes[0]"));

            // Expected outcome: `syntaxResponse.StatusCode` has the required value.
            // Acceptance criteria: `syntaxResponse.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // validation reports semantic paths and syntax locations.
            Assert.That(syntaxResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Expected outcome: `syntaxError?.Code` has the required value.
            // Acceptance criteria: `syntaxError?.Code` must equal `"yaml_syntax"`, because this condition proves that
            // validation reports semantic paths and syntax locations.
            Assert.That(syntaxError?.Code, Is.EqualTo("yaml_syntax"));

            // Expected outcome: `syntaxError?.Details?.ToString(` includes the required content.
            // Acceptance criteria: `syntaxError?.Details?.ToString(` must contain `"line"`, because this condition proves that
            // validation reports semantic paths and syntax locations.
            Assert.That(syntaxError?.Details?.ToString(), Does.Contain("line"));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that list is deterministic and includes default.
    /// Description: Arranges the inputs for list is deterministic and includes default, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

            // Expected outcome: `response.StatusCode` has the required value.
            // Acceptance criteria: `response.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
            // list is deterministic and includes default.
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        var json = await client.GetFromJsonAsync<TemplateList>(
            "/api/controller-templates",
            FlowControlJson.Options);

        // Expected outcome: `json?.Items.Select(item => item.Id` has the required value.
        // Acceptance criteria: `json?.Items.Select(item => item.Id` must equal `new[] { "default", "alpha", "zulu" }`, because this condition proves that
        // list is deterministic and includes default.
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