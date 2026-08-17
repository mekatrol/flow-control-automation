using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Tests.Unit.Points;

[TestFixture]
internal sealed class PointDefinitionEndpointTests
{
    /// <summary>
    /// Purpose: Protects the behavioral contract that point and group crud uses canonical yaml and revisions.
    /// Description: Arranges the inputs for point and group crud uses canonical yaml and revisions, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task PointAndGroupCrudUsesCanonicalYamlAndRevisions()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // point and group crud uses canonical yaml and revisions.
        Assert.Multiple(() =>
        {
            // Expected outcome: `created.GroupId` has the required value.
            // Acceptance criteria: `created.GroupId` must equal `"plant"`, because this condition proves that
            // point and group crud uses canonical yaml and revisions.
            Assert.That(created.GroupId, Is.EqualTo("plant"));

            // Expected outcome: The observed result satisfies the required contract.
            // Acceptance criteria: the asserted condition must hold, because this condition proves that
            // point and group crud uses canonical yaml and revisions.
            Assert.That(created.Revision, Is.Zero);

            // Expected outcome: `created.CreatedAt` is absent.
            // Acceptance criteria: `created.CreatedAt` must be null, because this condition proves that
            // point and group crud uses canonical yaml and revisions.
            Assert.That(created.CreatedAt, Is.Null);
        });

        using var get = await client.GetAsync("/api/points/supply");
        AssertResource(get, HttpStatusCode.OK, revision: 1);

        using var update = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/points/supply",
            PointYaml.Render(created with { Id = "renamed-supply", Name = "Updated supply" }),
            revision: 1);
        AssertResource(update, HttpStatusCode.OK, revision: 2);

        using var oldId = await client.GetAsync("/api/points/supply");
        Assert.That(oldId.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var renamed = await client.GetAsync("/api/points/renamed-supply");
        AssertResource(renamed, HttpStatusCode.OK, revision: 2);

        using var stale = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/points/renamed-supply",
            PointYaml.Render(created with { Id = "renamed-supply" }),
            revision: 1);
        await AssertError(stale, HttpStatusCode.Conflict, "stale_revision");

        using var occupiedDelete =
            await client.DeleteAsync("/api/point-groups/plant?revision=1");
        var conflict = await occupiedDelete.Content.ReadFromJsonAsync<JsonElement>();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // point and group crud uses canonical yaml and revisions.
        Assert.Multiple(() =>
        {
            // Expected outcome: `occupiedDelete.StatusCode` has the required value.
            // Acceptance criteria: `occupiedDelete.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
            // point and group crud uses canonical yaml and revisions.
            Assert.That(occupiedDelete.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            // Expected outcome: `conflict.GetProperty("code"` has the required value.
            // Acceptance criteria: `conflict.GetProperty("code"` must equal `"resource_conflict"`, because this condition proves that
            // point and group crud uses canonical yaml and revisions.
            Assert.That(conflict.GetProperty("code").GetString(), Is.EqualTo("resource_conflict"));

            // Expected outcome: `conflict.GetProperty("details"` has the required value.
            // Acceptance criteria: `conflict.GetProperty("details"` must equal `"supply"`, because this condition proves that
            // point and group crud uses canonical yaml and revisions.
            Assert.That(
                conflict.GetProperty("details").GetProperty("pointIds")[0].GetString(),
                Is.EqualTo("renamed-supply"));
        });

        using var standalone = await client.PostAsync(
            "/api/point-groups/plant/make-points-standalone?revision=1",
            content: null);
        var standaloneBody =
            await standalone.Content.ReadFromJsonAsync<JsonElement>();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // point and group crud uses canonical yaml and revisions.
        Assert.Multiple(() =>
        {
            // Expected outcome: `standalone.StatusCode` has the required value.
            // Acceptance criteria: `standalone.StatusCode` must equal `HttpStatusCode.OK`, because this condition proves that
            // point and group crud uses canonical yaml and revisions.
            Assert.That(standalone.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Expected outcome: `standaloneBody.GetProperty("updatedItems"` has the required value.
            // Acceptance criteria: `standaloneBody.GetProperty("updatedItems"` must equal `1`, because this condition proves that
            // point and group crud uses canonical yaml and revisions.
            Assert.That(standaloneBody.GetProperty("updatedItems").GetInt32(), Is.EqualTo(1));

            // Expected outcome: `standaloneBody.GetProperty("items"` rejects the prohibited condition.
            // Acceptance criteria: `standaloneBody.GetProperty("items"` must be false, because this condition proves that
            // point and group crud uses canonical yaml and revisions.
            Assert.That(
                standaloneBody.GetProperty("items")[0].TryGetProperty("groupId", out _),
                Is.False);
        });

        using var deletePoint = await client.DeleteAsync("/api/points/renamed-supply?revision=3");

        // Expected outcome: `deletePoint.StatusCode` has the required value.
        // Acceptance criteria: `deletePoint.StatusCode` must equal `HttpStatusCode.NoContent`, because this condition proves that
        // point and group crud uses canonical yaml and revisions.
        Assert.That(deletePoint.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using var deleteGroup =
            await client.DeleteAsync("/api/point-groups/plant?revision=1");

        // Expected outcome: `deleteGroup.StatusCode` has the required value.
        // Acceptance criteria: `deleteGroup.StatusCode` must equal `HttpStatusCode.NoContent`, because this condition proves that
        // point and group crud uses canonical yaml and revisions.
        Assert.That(deleteGroup.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that lists filter sort and paginate deterministically.
    /// Description: Arranges the inputs for lists filter sort and paginate deterministically, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task ListsFilterSortAndPaginateDeterministically()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var group = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-groups",
            PointGroupYaml.Render(Group("plant", "Plant")));

        // Expected outcome: `group.StatusCode` has the required value.
        // Acceptance criteria: `group.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
        // lists filter sort and paginate deterministically.
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

            // Expected outcome: `created.StatusCode` has the required value.
            // Acceptance criteria: `created.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
            // lists filter sort and paginate deterministically.
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // lists filter sort and paginate deterministically.
        Assert.Multiple(() =>
        {
            // Expected outcome: `page!.TotalItems` has the required value.
            // Acceptance criteria: `page!.TotalItems` must equal `12`, because this condition proves that
            // lists filter sort and paginate deterministically.
            Assert.That(page!.TotalItems, Is.EqualTo(12));

            // Expected outcome: `page.PageCount` has the required value.
            // Acceptance criteria: `page.PageCount` must equal `2`, because this condition proves that
            // lists filter sort and paginate deterministically.
            Assert.That(page.PageCount, Is.EqualTo(2));

            // Expected outcome: `page.Items.Select(item => item.Name` has the required value.
            // Acceptance criteria: `page.Items.Select(item => item.Name` must equal `new[] { "Temperature 02", "Temperature 01" }`, because this condition proves that
            // lists filter sort and paginate deterministically.
            Assert.That(page.Items.Select(item => item.Name),
                Is.EqualTo(new[] { "Temperature 02", "Temperature 01" }));

            // Expected outcome: `grouped!.TotalItems` has the required value.
            // Acceptance criteria: `grouped!.TotalItems` must equal `6`, because this condition proves that
            // lists filter sort and paginate deterministically.
            Assert.That(grouped!.TotalItems, Is.EqualTo(6));

            // Expected outcome: `standalone!.TotalItems` has the required value.
            // Acceptance criteria: `standalone!.TotalItems` must equal `6`, because this condition proves that
            // lists filter sort and paginate deterministically.
            Assert.That(standalone!.TotalItems, Is.EqualTo(6));

            // Expected outcome: `page.Items[0].Revision` has the required value.
            // Acceptance criteria: `page.Items[0].Revision` must equal `1`, because this condition proves that
            // lists filter sort and paginate deterministically.
            Assert.That(page.Items[0].Revision, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that lists reject malformed queries.
    /// Description: Arranges the inputs for lists reject malformed queries, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [TestCase("/api/points?page=0")]
    [TestCase("/api/points?pageSize=100")]
    [TestCase("/api/points?sort=nope")]
    [TestCase("/api/points?groupId=a&groupId=b")]
    [TestCase("/api/point-groups?page=nope")]
    public async Task ListsRejectMalformedQueries(string path)
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        await AssertError(response, HttpStatusCode.BadRequest, "invalid_query");
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that strict yaml body limits and shape are enforced.
    /// Description: Arranges the inputs for strict yaml body limits and shape are enforced, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task StrictYamlBodyLimitsAndShapeAreEnforced()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
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

    /// <summary>
    /// Purpose: Protects the behavioral contract that unknown resources, invalid revisions, and point renames are stable.
    /// Description: Arranges unknown resources, invalid revisions, and a point rename, exercises the relevant operations,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task UnknownResourcesInvalidRevisionsAndPointRenamesAreStable()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
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

        // Expected outcome: `created.StatusCode` has the required value.
        // Acceptance criteria: `created.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
        // unknown resources invalid revisions and path mismatch are stable.
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var rename = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/points/point",
            PointYaml.Render(Point("other", "Other")),
            revision: 1);
        AssertResource(rename, HttpStatusCode.OK, revision: 2);
        using var oldId = await client.GetAsync("/api/points/point");
        await AssertError(oldId, HttpStatusCode.NotFound, "not_found");
        using var newId = await client.GetAsync("/api/points/other");
        AssertResource(newId, HttpStatusCode.OK, revision: 2);
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that runtime envelope never fabricates an uninitialized value.
    /// Description: Arranges the inputs for runtime envelope never fabricates an uninitialized value, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task RuntimeEnvelopeNeverFabricatesAnUninitializedValue()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var created = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/points",
            PointYaml.Render(Point("virtual-value", "Virtual value")));

        // Expected outcome: `created.StatusCode` has the required value.
        // Acceptance criteria: `created.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
        // runtime envelope never fabricates an uninitialized value.
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var runtime = await client.GetFromJsonAsync<PointRuntimeEnvelope>(
            "/api/points/virtual-value/runtime",
            FlowControlJson.Options);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // runtime envelope never fabricates an uninitialized value.
        Assert.Multiple(() =>
        {
            // Expected outcome: `runtime?.Value` is absent.
            // Acceptance criteria: `runtime?.Value` must be null, because this condition proves that
            // runtime envelope never fabricates an uninitialized value.
            Assert.That(runtime?.Value, Is.Null);

            // Expected outcome: `runtime?.Status` has the required value.
            // Acceptance criteria: `runtime?.Status` must equal `"unavailable"`, because this condition proves that
            // runtime envelope never fabricates an uninitialized value.
            Assert.That(runtime?.Status, Is.EqualTo("unavailable"));

            // Expected outcome: `runtime?.Quality` has the required value.
            // Acceptance criteria: `runtime?.Quality` must equal `"unavailable"`, because this condition proves that
            // runtime envelope never fabricates an uninitialized value.
            Assert.That(runtime?.Quality, Is.EqualTo(DataQuality.Unavailable));

            // Expected outcome: `runtime?.Reliability` has the required value.
            // Acceptance criteria: `runtime?.Reliability` must equal `"not_initialized"`, because this condition proves that
            // runtime envelope never fabricates an uninitialized value.
            Assert.That(runtime?.Reliability, Is.EqualTo("not_initialized"));

            // Expected outcome: `runtime?.Diagnostic` includes the required content.
            // Acceptance criteria: `runtime?.Diagnostic` must contain `"no commissioned runtime value"`, because this condition proves that
            // runtime envelope never fabricates an uninitialized value.
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
        Direction = DataDirection.Value,
        ValueType = PointValueType.Analog,
        Readable = true,
        Persistence = "volatile"
    };

    private static PointGroup Group(string id, string name) => new()
    {
        Id = id,
        Name = name
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
            Content = new StringContent(yaml, Encoding.UTF8, "application/yaml")
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
        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // assert resource.
        Assert.Multiple(() =>
        {
            // Expected outcome: `response.StatusCode` has the required value.
            // Acceptance criteria: `response.StatusCode` must equal `status`, because this condition proves that
            // assert resource.
            Assert.That(response.StatusCode, Is.EqualTo(status));

            // Expected outcome: `response.Content.Headers.ContentType?.MediaType` has the required value.
            // Acceptance criteria: `response.Content.Headers.ContentType?.MediaType` must equal `"application/yaml"`, because this condition proves that
            // assert resource.
            Assert.That(
                response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/yaml"));

            // Expected outcome: `response.Headers.GetValues("ETag"` has the required value.
            // Acceptance criteria: `response.Headers.GetValues("ETag"` must equal `new[] { revision.ToString() }`, because this condition proves that
            // assert resource.
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // assert error.
        Assert.Multiple(() =>
        {
            // Expected outcome: `response.StatusCode` has the required value.
            // Acceptance criteria: `response.StatusCode` must equal `status`, because this condition proves that
            // assert error.
            Assert.That(response.StatusCode, Is.EqualTo(status));

            // Expected outcome: `error.GetProperty("code"` has the required value.
            // Acceptance criteria: `error.GetProperty("code"` must equal `code`, because this condition proves that
            // assert error.
            Assert.That(error.GetProperty("code").GetString(), Is.EqualTo(code));

            // Expected outcome: The observed result satisfies the required contract.
            // Acceptance criteria: the asserted condition must hold, because this condition proves that
            // assert error.
            Assert.That(error.GetProperty("message").GetString(), Is.Not.Empty);
        });
    }
}
