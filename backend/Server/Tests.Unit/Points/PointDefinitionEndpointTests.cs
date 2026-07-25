using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Tests.Unit.Points;

[TestFixture]
internal sealed class PointDefinitionEndpointTests
{
    [Test]
    public async Task PointAndGroupCrudUsesCanonicalYamlAndRevisions()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        using var createGroup = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-groups",
            PointGroupYaml.Render(Group("plant", "Plant")));
        AssertResource(createGroup, HttpStatusCode.Created, revision: 1);

        using var createPoint = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/points",
            PointYaml.Render(Point("supply", "Supply temperature", "plant")));
        AssertResource(createPoint, HttpStatusCode.Created, revision: 1);
        var created = PointYaml.Parse(
            await createPoint.Content.ReadAsByteArrayAsync());
        Assert.Multiple(() =>
        {
            Assert.That(created.GroupId, Is.EqualTo("plant"));
            Assert.That(created.Revision, Is.Zero);
            Assert.That(created.CreatedAt, Is.Null);
        });

        using var get = await client.GetAsync("/api/points/supply");
        AssertResource(get, HttpStatusCode.OK, revision: 1);

        using var update = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/points/supply",
            PointYaml.Render(created with { Name = "Updated supply" }),
            revision: 1);
        AssertResource(update, HttpStatusCode.OK, revision: 2);

        using var stale = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/points/supply",
            PointYaml.Render(created),
            revision: 1);
        await AssertError(stale, HttpStatusCode.Conflict, "stale_revision");

        using var occupiedDelete =
            await client.DeleteAsync("/api/point-groups/plant?revision=1");
        var conflict = await occupiedDelete.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(occupiedDelete.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(conflict.GetProperty("code").GetString(), Is.EqualTo("resource_conflict"));
            Assert.That(
                conflict.GetProperty("details").GetProperty("pointIds")[0].GetString(),
                Is.EqualTo("supply"));
        });

        using var standalone = await client.PostAsync(
            "/api/point-groups/plant/make-points-standalone?revision=1",
            content: null);
        var standaloneBody =
            await standalone.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(standalone.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(standaloneBody.GetProperty("updatedItems").GetInt32(), Is.EqualTo(1));
            Assert.That(
                standaloneBody.GetProperty("items")[0].TryGetProperty("groupId", out _),
                Is.False);
        });

        using var deletePoint = await client.DeleteAsync("/api/points/supply?revision=3");
        Assert.That(deletePoint.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using var deleteGroup =
            await client.DeleteAsync("/api/point-groups/plant?revision=1");
        Assert.That(deleteGroup.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task ListsFilterSortAndPaginateDeterministically()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var group = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-groups",
            PointGroupYaml.Render(Group("plant", "Plant")));
        Assert.That(group.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        for (var index = 1; index <= 12; index++)
        {
            using var created = await SendYaml(
                client,
                HttpMethod.Post,
                "/api/points",
                PointYaml.Render(Point(
                    $"temperature-{index}",
                    $"Temperature {index:00}",
                    index % 2 == 0 ? "plant" : null)));
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        var page = await client.GetFromJsonAsync<PaginatedResult<Point>>(
            "/api/points?page=2&pageSize=10&filter=TEMPERATURE&sort=descending",
            FlowControlJson.Options);
        var grouped = await client.GetFromJsonAsync<PaginatedResult<Point>>(
            "/api/points?pageSize=10&groupId=plant",
            FlowControlJson.Options);
        var standalone = await client.GetFromJsonAsync<PaginatedResult<Point>>(
            "/api/points?pageSize=10&groupId=",
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(page!.TotalItems, Is.EqualTo(12));
            Assert.That(page.PageCount, Is.EqualTo(2));
            Assert.That(page.Items.Select(item => item.Name),
                Is.EqualTo(new[] { "Temperature 02", "Temperature 01" }));
            Assert.That(grouped!.TotalItems, Is.EqualTo(6));
            Assert.That(standalone!.TotalItems, Is.EqualTo(6));
            Assert.That(page.Items[0].Revision, Is.EqualTo(1));
        });
    }

    [TestCase("/api/points?page=0")]
    [TestCase("/api/points?pageSize=100")]
    [TestCase("/api/points?sort=nope")]
    [TestCase("/api/points?groupId=a&groupId=b")]
    [TestCase("/api/point-groups?page=nope")]
    public async Task ListsRejectMalformedQueries(string path)
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        await AssertError(response, HttpStatusCode.BadRequest, "invalid_query");
    }

    [Test]
    public async Task StrictYamlBodyLimitsAndShapeAreEnforced()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        const string duplicate = """
            schemaVersion: 1
            groups: []
            points:
              - id: point
                name: Point
                name: Duplicate
                enabled: true
                implementation: virtual
                direction: value
                valueType: analog
                readable: true
                commandable: false
                persistence: volatile
            """;
        using var duplicateResponse =
            await SendYaml(client, HttpMethod.Post, "/api/points", duplicate);
        await AssertError(duplicateResponse, HttpStatusCode.BadRequest, "unsupported_yaml");

        const string trailing = """
            schemaVersion: 1
            groups: []
            points: []
            ---
            schemaVersion: 1
            groups: []
            points: []
            """;
        using var trailingResponse =
            await SendYaml(client, HttpMethod.Post, "/api/points", trailing);
        await AssertError(
            trailingResponse,
            HttpStatusCode.BadRequest,
            "multiple_yaml_documents");

        using var wrongShape = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/points",
            PointGroupYaml.Render(Group("group", "Group")));
        await AssertError(wrongShape, HttpStatusCode.BadRequest, "invalid_yaml");

        var oversized = new string(' ', ConfigurationYaml.MaximumBytes + 1);
        using var oversizedResponse =
            await SendYaml(client, HttpMethod.Post, "/api/points", oversized);
        await AssertError(
            oversizedResponse,
            HttpStatusCode.BadRequest,
            "request_too_large");
    }

    [Test]
    public async Task UnknownResourcesInvalidRevisionsAndPathMismatchAreStable()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var missing = await client.GetAsync("/api/points/missing");
        await AssertError(missing, HttpStatusCode.NotFound, "not_found");

        using var invalidRevision = await client.DeleteAsync("/api/points/missing?revision=no");
        await AssertError(
            invalidRevision,
            HttpStatusCode.BadRequest,
            "invalid_revision");

        using var created = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/points",
            PointYaml.Render(Point("point", "Point")));
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var mismatch = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/points/point",
            PointYaml.Render(Point("other", "Other")),
            revision: 1);
        await AssertError(mismatch, HttpStatusCode.BadRequest, "validation_failed");
    }

    [Test]
    public async Task RuntimeEnvelopeNeverFabricatesAnUninitializedValue()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var created = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/points",
            PointYaml.Render(Point("virtual-value", "Virtual value")));
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var runtime = await client.GetFromJsonAsync<PointRuntimeEnvelope>(
            "/api/points/virtual-value/runtime",
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(runtime?.Value, Is.Null);
            Assert.That(runtime?.Status, Is.EqualTo("unavailable"));
            Assert.That(runtime?.Quality, Is.EqualTo("unavailable"));
            Assert.That(runtime?.Reliability, Is.EqualTo("not_initialized"));
            Assert.That(runtime?.Diagnostic, Does.Contain("no commissioned runtime value"));
        });

        using var missing = await client.GetAsync("/api/points/missing/runtime");
        await AssertError(missing, HttpStatusCode.NotFound, "not_found");
    }

    private static Point Point(string id, string name, string? groupId = null) => new()
    {
        Id = id,
        Name = name,
        Enabled = true,
        GroupId = groupId,
        Implementation = "virtual",
        Direction = "value",
        ValueType = "analog",
        Readable = true,
        Persistence = "volatile",
    };

    private static PointGroup Group(string id, string name) => new()
    {
        Id = id,
        Name = name,
    };

    private static async Task<HttpResponseMessage> SendYaml(
        HttpClient client,
        HttpMethod method,
        string path,
        string yaml,
        int? revision = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(yaml, Encoding.UTF8, "application/yaml"),
        };
        if (revision is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", revision.ToString());
        }

        return await client.SendAsync(request);
    }

    private static void AssertResource(
        HttpResponseMessage response,
        HttpStatusCode status,
        int revision)
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(status));
            Assert.That(
                response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/yaml"));
            Assert.That(
                response.Headers.GetValues("ETag"),
                Is.EqualTo(new[] { revision.ToString() }));
        });
    }

    private static async Task AssertError(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(status));
            Assert.That(error.GetProperty("code").GetString(), Is.EqualTo(code));
            Assert.That(error.GetProperty("message").GetString(), Is.Not.Empty);
        });
    }
}