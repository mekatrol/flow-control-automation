using Server.Common.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Tests.Unit.Api;

[TestFixture]
internal sealed class ApiAccessEndpointTests
{
    [TestCase("/")]
    [TestCase("/index.html")]
    [TestCase("/flows/example")]
    public async Task FrontendEntryPointsReceiveTheConfiguredApiKey(string path)
    {
        const string index = "<meta name=\"flow-control-api-key\" content=\"__FLOW_CONTROL_API_KEY__\">";
        await using var factory = new FlowControlApplicationFactory(
            environment: "Production",
            frontendIndex: index);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
            Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
            Assert.That(html, Does.Contain("content=\"test-api-key\""));
            Assert.That(html, Does.Not.Contain("__FLOW_CONTROL_API_KEY__"));
        });
    }

    [Test]
    public async Task UnknownApiRoutesDoNotReturnTheFrontendBundle()
    {
        const string index = "<meta name=\"flow-control-api-key\" content=\"__FLOW_CONTROL_API_KEY__\">";
        await using var factory = new FlowControlApplicationFactory(
            environment: "Production",
            frontendIndex: index);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");

        var response = await client.GetAsync("/api/not-an-endpoint");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ProductionApiRequiresAConfiguredKey()
    {
        await using var factory = new FlowControlApplicationFactory(environment: "Production");
        using var client = factory.CreateClient();

        Assert.That((await client.GetAsync("/api/execution-contexts")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        Assert.That((await client.GetAsync("/api/execution-contexts")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task MutationsCreateDurableActorQualifiedAuditRecords()
    {
        await using var factory = new FlowControlApplicationFactory(environment: "Production");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");

        var response = await client.PostAsJsonAsync("/api/execution-contexts", new ExecutionContextDefinition { Id = "audited", Name = "Audited" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var json = await client.GetStringAsync("/api/audit-records");
        using var document = JsonDocument.Parse(json);
        var record = document.RootElement.EnumerateArray().Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.GetProperty("actor").GetString(), Is.EqualTo("test"));
            Assert.That(record.GetProperty("method").GetString(), Is.EqualTo("POST"));
            Assert.That(record.GetProperty("path").GetString(), Is.EqualTo("/api/execution-contexts"));
            Assert.That(record.GetProperty("statusCode").GetInt32(), Is.EqualTo(201));
        });
    }
}