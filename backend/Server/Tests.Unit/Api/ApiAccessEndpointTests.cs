using Server.Common.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Tests.Unit.Api;

[TestFixture]
internal sealed class ApiAccessEndpointTests
{
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