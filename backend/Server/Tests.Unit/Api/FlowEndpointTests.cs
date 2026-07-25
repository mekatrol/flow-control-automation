using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Tests.Unit.Api;

[TestFixture]
internal sealed class FlowEndpointTests
{
    [Test]
    public async Task CrudPersistsAcrossApplicationRestart()
    {
        using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var created = await CreateFlow(client, "Climate Control");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Id, Is.EqualTo("climate-control"));
            Assert.That(created.Status, Is.EqualTo("draft"));
            Assert.That(created.Nodes, Is.Empty);
        }

        var changed = created with
        {
            Name = "Renamed climate",
            Description = "Persisted graph",
            Nodes =
            [
                new FlowNode
                {
                    Id = "pulse-1",
                    Kind = "pulse",
                    Label = "Every minute",
                    X = 10,
                    Y = 20,
                    ZOrder = 1,
                    Configuration = new Dictionary<string, JsonElement>
                    {
                        ["interval"] = JsonSerializer.SerializeToElement(60),
                    },
                },
            ],
        };
        using var saveResponse = await client.PutAsJsonAsync(
            $"/api/flows/{created.Id}",
            changed,
            FlowControlJson.Options);
        Assert.That(saveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var secondClient = factory.CreateClient();
        var loaded = await secondClient.GetFromJsonAsync<Flow>(
            $"/api/flows/{created.Id}",
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Description, Is.EqualTo("Persisted graph"));
            Assert.That(loaded.Nodes, Has.Count.EqualTo(1));
        });

        using var deleteResponse = await secondClient.DeleteAsync($"/api/flows/{created.Id}");
        Assert.Multiple(() =>
        {
            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(deleteResponse.Content.Headers.ContentLength, Is.EqualTo(0));
        });
        using var missing = await secondClient.GetAsync($"/api/flows/{created.Id}");
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CreateMakesUniqueReadableIds()
    {
        using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var first = await CreateFlow(client, "Heating & Cooling");
        var second = await CreateFlow(client, "Heating & Cooling");
        Assert.Multiple(() =>
        {
            Assert.That(first.Id, Is.EqualTo("heating-cooling"));
            Assert.That(second.Id, Is.EqualTo("heating-cooling-2"));
        });
    }

    [Test]
    public async Task ListFiltersSortsPaginatesAndClampsPage()
    {
        using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        for (var index = 1; index <= 25; index++)
        {
            await CreateFlow(client, $"Flow {index:00}");
        }

        var page = await client.GetFromJsonAsync<PaginatedResult<Flow>>(
            "/api/flows?page=2&pageSize=10&filter=flow&sort=descending",
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(page, Is.Not.Null);
            Assert.That(page!.TotalItems, Is.EqualTo(25));
            Assert.That(page.PageCount, Is.EqualTo(3));
            Assert.That(page.Page, Is.EqualTo(2));
            Assert.That(page.Items, Has.Count.EqualTo(10));
            Assert.That(page.Items[0].Name, Is.EqualTo("Flow 15"));
            Assert.That(page.Items[9].Name, Is.EqualTo("Flow 06"));
        });

        var clamped = await client.GetFromJsonAsync<PaginatedResult<Flow>>(
            "/api/flows?page=99&pageSize=10",
            FlowControlJson.Options);
        Assert.That(clamped!.Page, Is.EqualTo(3));
    }

    [TestCase("/api/flows?page=0", "page must be a positive integer")]
    [TestCase("/api/flows?page=nope", "page must be a positive integer")]
    [TestCase("/api/flows?pageSize=100", "pageSize must be 10, 20, or 50")]
    [TestCase("/api/flows?sort=sideways", "sort must be ascending or descending")]
    [TestCase("/api/flows?status=paused", "each status must be draft or deployed")]
    public async Task ListRejectsInvalidQueries(string path, string message)
    {
        using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
            Assert.That(error!["message"], Is.EqualTo(message));
        });
    }

    [Test]
    public async Task SaveRejectsUnknownFieldsTrailingValuesAndMismatchedId()
    {
        using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var created = await CreateFlow(client, "Safe flow");

        using var unknown = await PutJson(
            client,
            "/api/flows/safe-flow",
            """{"id":"safe-flow","name":"Safe flow","updatedAt":"2026-01-01T00:00:00Z","unknown":true}""");
        using var trailing = await PutJson(
            client,
            "/api/flows/safe-flow",
            """{"id":"safe-flow","name":"Safe flow","updatedAt":"2026-01-01T00:00:00Z"} {}""");
        using var mismatch = await client.PutAsJsonAsync(
            "/api/flows/safe-flow",
            created with { Id = "different" },
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(trailing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(mismatch.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task RuntimeStartsStoppedDeploysAndHonorsDisableEnable()
    {
        using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var created = await CreateFlow(client, "Runtime flow");

        var stopped = await client.GetFromJsonAsync<RuntimeSnapshot>(
            $"/api/flows/{created.Id}/runtime",
            FlowControlJson.Options);
        using var deployResponse = await client.PostAsync(
            $"/api/flows/{created.Id}/deploy",
            content: null);
        var running = await deployResponse.Content.ReadFromJsonAsync<RuntimeSnapshot>(
            FlowControlJson.Options);
        using var disableResponse = await client.PostAsync(
            $"/api/flows/{created.Id}/disable",
            content: null);
        var disabled = await disableResponse.Content.ReadFromJsonAsync<Flow>(
            FlowControlJson.Options);
        using var disabledDeployResponse = await client.PostAsync(
            $"/api/flows/{created.Id}/deploy",
            content: null);
        var disabledRuntime =
            await disabledDeployResponse.Content.ReadFromJsonAsync<RuntimeSnapshot>(
                FlowControlJson.Options);
        using var enableResponse = await client.PostAsync(
            $"/api/flows/{created.Id}/enable",
            content: null);
        var enabled = await enableResponse.Content.ReadFromJsonAsync<Flow>(
            FlowControlJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(stopped!.State, Is.EqualTo("stopped"));
            Assert.That(stopped.Nodes, Is.Not.Null);
            Assert.That(running!.State, Is.EqualTo("running"));
            Assert.That(disabled!.Disabled, Is.True);
            Assert.That(disabledRuntime!.State, Is.EqualTo("stopped"));
            Assert.That(enabled!.Disabled, Is.False);
        });
    }

    [Test]
    public async Task RuntimeRoutesReturnNotFoundForMissingFlow()
    {
        using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var get = await client.GetAsync("/api/flows/missing/runtime");
        using var deploy = await client.PostAsync("/api/flows/missing/deploy", content: null);
        Assert.Multiple(() =>
        {
            Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(deploy.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    private static async Task<Flow> CreateFlow(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/flows",
            new { name },
            FlowControlJson.Options);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        return (await response.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options))!;
    }

    private static Task<HttpResponseMessage> PutJson(
        HttpClient client,
        string path,
        string json) =>
        client.PutAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
}