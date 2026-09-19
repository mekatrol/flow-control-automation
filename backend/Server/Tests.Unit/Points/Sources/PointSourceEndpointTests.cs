using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Tests.Unit.Points.Sources;

[TestFixture]
internal sealed class PointSourceEndpointTests
{
    [Test]
    public async Task PointIdsAreUniqueWithinTheirSourceAndReusableAcrossSources()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var template = PointSourceYaml.Parse(File.ReadAllBytes(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "ContractFixtures",
            "point-sources",
            "valid",
            "virtual.v1.yaml")));
        var first = template with { Id = "panel-a", Name = "Panel A" };
        var second = template with { Id = "panel-b", Name = "Panel B" };

        using var firstResponse = await SendYaml(client, HttpMethod.Post, "/api/point-sources", first);
        using var secondResponse = await SendYaml(client, HttpMethod.Post, "/api/point-sources", second);
        using var duplicateResponse = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-sources",
            template with
            {
                Id = "panel-c",
                Name = "Panel C",
                Points = [.. template.Points, template.Points[0]]
            });

        Assert.Multiple(() =>
        {
            Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(duplicateResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [TestCase("POST", "/api/points")]
    [TestCase("GET", "/api/points/example")]
    [TestCase("PUT", "/api/points/example")]
    [TestCase("DELETE", "/api/points/example?revision=1")]
    public async Task StandalonePointDefinitionRoutesAreRemoved(string method, string path)
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that crud uses yaml etags and revision conflicts.
    /// Description: Arranges the inputs for crud uses yaml etags and revision conflicts, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task CrudUsesYamlEtagsAndRevisionConflicts()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var source = ValidHttpSource();

        using var create = await SendYaml(client, HttpMethod.Post, "/api/point-sources", source);
        var created = await ReadSource(create);

        // Expected outcome: `create.Headers.TryGetValues("ETag"` confirms the required condition.
        // Acceptance criteria: `create.Headers.TryGetValues("ETag"` must be true, because this condition proves that
        // crud uses yaml etags and revision conflicts.
        Assert.That(create.Headers.TryGetValues("ETag", out var createEtags), Is.True);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // crud uses yaml etags and revision conflicts.
        Assert.Multiple(() =>
        {
            // Expected outcome: `create.StatusCode` has the required value.
            // Acceptance criteria: `create.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created));

            // Expected outcome: `create.Content.Headers.ContentType?.MediaType` has the required value.
            // Acceptance criteria: `create.Content.Headers.ContentType?.MediaType` must equal `"application/yaml"`, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(create.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/yaml"));

            // Expected outcome: `createEtags` has the required value.
            // Acceptance criteria: `createEtags` must equal `new[] { "1" }`, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(createEtags, Is.EqualTo(new[] { "1" }));

            // Expected outcome: The observed result satisfies the required contract.
            // Acceptance criteria: the asserted condition must hold, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(created.Revision, Is.Zero);

            // Expected outcome: `created.CreatedAt` is absent.
            // Acceptance criteria: `created.CreatedAt` must be null, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(created.CreatedAt, Is.Null);

            // Expected outcome: `created.UpdatedAt` is absent.
            // Acceptance criteria: `created.UpdatedAt` must be null, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(created.UpdatedAt, Is.Null);
        });

        using var get = await client.GetAsync("/api/point-sources/weather");

        using (Assert.EnterMultipleScope())
        {
            // Expected outcome: `get.Headers.TryGetValues("ETag"` confirms the required condition.
            // Acceptance criteria: `get.Headers.TryGetValues("ETag"` must be true, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(get.Headers.TryGetValues("ETag", out var getEtags), Is.True);

            // Expected outcome: `getEtags` has the required value.
            // Acceptance criteria: `getEtags` must equal `new[] { "1" }`, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(getEtags, Is.EqualTo(new[] { "1" }));
        }

        var updatedInput = source with { Name = "Updated weather" };
        using var update = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/point-sources/weather",
            updatedInput,
            revision: 1);
        var updated = await ReadSource(update);

        // Expected outcome: `update.Headers.TryGetValues("ETag"` confirms the required condition.
        // Acceptance criteria: `update.Headers.TryGetValues("ETag"` must be true, because this condition proves that
        // crud uses yaml etags and revision conflicts.
        Assert.That(update.Headers.TryGetValues("ETag", out var updateEtags), Is.True);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // crud uses yaml etags and revision conflicts.
        Assert.Multiple(() =>
        {
            // Expected outcome: `update.StatusCode` has the required value.
            // Acceptance criteria: `update.StatusCode` must equal `HttpStatusCode.OK`, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Expected outcome: `updateEtags` has the required value.
            // Acceptance criteria: `updateEtags` must equal `new[] { "2" }`, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(updateEtags, Is.EqualTo(new[] { "2" }));

            // Expected outcome: `updated.Name` has the required value.
            // Acceptance criteria: `updated.Name` must equal `"Updated weather"`, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(updated.Name, Is.EqualTo("Updated weather"));
        });

        using var staleUpdate = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/point-sources/weather",
            source,
            revision: 1);

        // Expected outcome: `staleUpdate.StatusCode` has the required value.
        // Acceptance criteria: `staleUpdate.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
        // crud uses yaml etags and revision conflicts.
        Assert.That(staleUpdate.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        using var staleDelete = await client.DeleteAsync(
            "/api/point-sources/weather?revision=1");

        // Expected outcome: `staleDelete.StatusCode` has the required value.
        // Acceptance criteria: `staleDelete.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
        // crud uses yaml etags and revision conflicts.
        Assert.That(staleDelete.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        using var delete = await client.DeleteAsync("/api/point-sources/weather?revision=2");

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // crud uses yaml etags and revision conflicts.
        Assert.Multiple(() =>
        {
            // Expected outcome: `delete.StatusCode` has the required value.
            // Acceptance criteria: `delete.StatusCode` must equal `HttpStatusCode.NoContent`, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            // Expected outcome: `delete.Content.Headers.ContentLength` has the required value.
            // Acceptance criteria: `delete.Content.Headers.ContentLength` must equal `0`, because this condition proves that
            // crud uses yaml etags and revision conflicts.
            Assert.That(delete.Content.Headers.ContentLength, Is.EqualTo(0));
        });
        using var missing = await client.GetAsync("/api/point-sources/weather");

        // Expected outcome: `missing.StatusCode` has the required value.
        // Acceptance criteria: `missing.StatusCode` must equal `HttpStatusCode.NotFound`, because this condition proves that
        // crud uses yaml etags and revision conflicts.
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    /// <summary>
    /// Purpose: Protects atomic point-source ID replacement through PUT.
    /// Description: Renames a source and verifies that only the new resource identifier remains.
    /// </summary>
    [Test]
    public async Task UpdateCanRenamePointSource()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var source = ValidHttpSource();

        using var create = await SendYaml(client, HttpMethod.Post, "/api/point-sources", source);
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var rename = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/point-sources/weather",
            source with { Id = "outdoor-weather" },
            revision: 1);
        using var oldResource = await client.GetAsync("/api/point-sources/weather");
        using var renamedResource = await client.GetAsync("/api/point-sources/outdoor-weather");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rename.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(oldResource.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(renamedResource.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }

    /// <summary>
    /// Purpose: Protects actionable duplicate-ID diagnostics during a rename.
    /// Description: Attempts to rename onto another source and verifies its name is reported.
    /// </summary>
    [Test]
    public async Task RenameRejectsExistingIdAndReportsExistingName()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var source = ValidHttpSource();
        var existing = source with { Id = "indoor-climate", Name = "Indoor Climate" };

        using var createSource = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-sources",
            source);
        using var createExisting = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-sources",
            existing);
        using var rename = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/point-sources/weather",
            source with { Id = existing.Id },
            revision: 1);
        var responseBody = await rename.Content.ReadAsStringAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(createSource.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(createExisting.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(rename.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(responseBody, Does.Contain(existing.Id));
            Assert.That(responseBody, Does.Contain(existing.Name));
        }
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that list filters sorts paginates and persists across scopes.
    /// Description: Arranges the inputs for list filters sorts paginates and persists across scopes, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task ListFiltersSortsPaginatesAndPersistsAcrossScopes()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        for (var index = 1; index <= 12; index++)
        {
            var source = ValidHttpSource() with
            {
                Id = $"weather-{index}",
                Name = $"Weather {index:00}"
            };
            using var response = await SendYaml(
                client,
                HttpMethod.Post,
                "/api/point-sources",
                source);

            // Expected outcome: `response.StatusCode` has the required value.
            // Acceptance criteria: `response.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
            // list filters sorts paginates and persists across scopes.
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        using var secondClient = factory.CreateClient();
        var page = await secondClient.GetFromJsonAsync<PaginatedResult<Server.Api.Contracts.PointSourceSummary>>(
            "/api/point-sources?page=2&pageSize=10&filter=WEATHER&sort=descending",
            FlowControlJson.Options);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // list filters sorts paginates and persists across scopes.
        Assert.Multiple(() =>
        {
            // Expected outcome: `page` is available.
            // Acceptance criteria: `page` must not be null, because this condition proves that
            // list filters sorts paginates and persists across scopes.
            Assert.That(page, Is.Not.Null);

            // Expected outcome: `page!.TotalItems` has the required value.
            // Acceptance criteria: `page!.TotalItems` must equal `12`, because this condition proves that
            // list filters sorts paginates and persists across scopes.
            Assert.That(page!.TotalItems, Is.EqualTo(12));

            // Expected outcome: `page.PageCount` has the required value.
            // Acceptance criteria: `page.PageCount` must equal `2`, because this condition proves that
            // list filters sorts paginates and persists across scopes.
            Assert.That(page.PageCount, Is.EqualTo(2));

            // Expected outcome: `page.Page` has the required value.
            // Acceptance criteria: `page.Page` must equal `2`, because this condition proves that
            // list filters sorts paginates and persists across scopes.
            Assert.That(page.Page, Is.EqualTo(2));

            // Expected outcome: `page.Items` contains the required number of entries.
            // Acceptance criteria: `page.Items` must contain exactly 2 entries, because this condition proves that
            // list filters sorts paginates and persists across scopes.
            Assert.That(page.Items, Has.Count.EqualTo(2));

            // Expected outcome: `page.Items[0].Name` has the required value.
            // Acceptance criteria: `page.Items[0].Name` must equal `"Weather 02"`, because this condition proves that
            // list filters sorts paginates and persists across scopes.
            Assert.That(page.Items[0].Name, Is.EqualTo("Weather 02"));

            // Expected outcome: `page.Items[1].Name` has the required value.
            // Acceptance criteria: `page.Items[1].Name` must equal `"Weather 01"`, because this condition proves that
            // list filters sorts paginates and persists across scopes.
            Assert.That(page.Items[1].Name, Is.EqualTo("Weather 01"));

            // Expected outcome: `page.Items[0].Revision` has the required value.
            // Acceptance criteria: `page.Items[0].Revision` must equal `1`, because this condition proves that
            // list filters sorts paginates and persists across scopes.
            Assert.That(page.Items[0].Revision, Is.EqualTo(1));

            // Expected outcome: `page.Items[0].CreatedAt` is available.
            // Acceptance criteria: `page.Items[0].CreatedAt` must not be null, because this condition proves that
            // list filters sorts paginates and persists across scopes.
            Assert.That(page.Items[0].UpdatedAt, Is.Not.Null);
            Assert.That(page.Items[0].MappingCount, Is.Zero);
            Assert.That(page.Items[0].PointCount, Is.Zero);
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that list rejects invalid queries.
    /// Description: Arranges the inputs for list rejects invalid queries, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [TestCase("/api/point-sources?page=0")]
    [TestCase("/api/point-sources?page=nope")]
    [TestCase("/api/point-sources?pageSize=100")]
    [TestCase("/api/point-sources?sort=sideways")]
    public async Task ListRejectsInvalidQueries(string path)
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // list rejects invalid queries.
        Assert.Multiple(() =>
        {
            // Expected outcome: `response.StatusCode` has the required value.
            // Acceptance criteria: `response.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // list rejects invalid queries.
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Expected outcome: `error!["message"]` has the required value.
            // Acceptance criteria: `error!["message"]` must equal `"invalid pagination or sort query"`, because this condition proves that
            // list rejects invalid queries.
            Assert.That(error!["message"], Is.EqualTo("invalid pagination or sort query"));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that validation and duplicate names roll back.
    /// Description: Arranges the inputs for validation and duplicate names roll back, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task ValidationAndDuplicateNamesRollBack()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var source = ValidHttpSource();
        using var created = await SendYaml(client, HttpMethod.Post, "/api/point-sources", source);

        // Expected outcome: `created.StatusCode` has the required value.
        // Acceptance criteria: `created.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
        // validation and duplicate names roll back.
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var duplicateName = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-sources",
            source with { Id = "forecast" });
        var duplicateNameError =
            await duplicateName.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        // Expected outcome: `duplicateName.StatusCode` has the required value.
        // Acceptance criteria: `duplicateName.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
        // validation and duplicate names roll back.
        Assert.That(duplicateName.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(
            duplicateNameError!["message"],
            Is.EqualTo(
                "A point source named \"Weather\" already exists with ID \"weather\". "
                + "Choose a different name."));

        using var duplicateId = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-sources",
            source);
        var duplicateIdError =
            await duplicateId.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.That(duplicateId.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(
            duplicateIdError!["message"],
            Is.EqualTo(
                "A point source with ID \"weather\" already exists with name \"Weather\". "
                + "Choose a different ID."));

        using var invalidResponseLimit = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-sources",
            source with
            {
                Id = "unsafe",
                Name = "Unsafe",
                Connection = source.Connection with { MaximumResponseBytes = 0 }
            });

        // Expected outcome: `invalidResponseLimit.StatusCode` has the required value.
        // Acceptance criteria: `invalidResponseLimit.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
        // validation and duplicate names roll back.
        Assert.That(invalidResponseLimit.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using var invalidPoint = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-sources",
            source with
            {
                Id = "invalid-point",
                Name = "Invalid point",
                Mappings = [new PointMapping
                {
                    Id = "output",
                    Aliases = ["value"],
                    Command = new MappingCommandOperation
                    {
                        Path = "/output",
                        Method = "POST",
                        PayloadFormat = "json",
                        Template = "{ \"value\": {{ value }} }"
                    }
                }],
                Points = [new AutomationPoint
                {
                    Id = "invalid-output",
                    Name = "Invalid output",
                    Enabled = true,
                    Direction = DataDirectionType.Output,
                    ValueType = AutomationPointValueType.Analog,
                    Commandable = true,
                    Persistence = "volatile",
                    Mapping = "output/value"
                }]
            });
        var invalidPointError =
            await invalidPoint.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.Multiple(() =>
        {
            Assert.That(invalidPoint.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(
                invalidPointError!["message"],
                Is.EqualTo("commandable points require safeDisablePolicy"));
        });

        using var loaded = await client.GetAsync("/api/point-sources/weather");

        // Expected outcome: `loaded.StatusCode` has the required value.
        // Acceptance criteria: `loaded.StatusCode` must equal `HttpStatusCode.OK`, because this condition proves that
        // validation and duplicate names roll back.
        Assert.That(loaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// Purpose: Ensures HTTP JSON sources can connect directly to devices on a private LAN.
    /// </summary>
    [Test]
    public async Task HttpSourceAllowsHttpBaseUrl()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var source = ValidHttpSource() with
        {
            Id = "lego-train",
            Name = "Lego train control",
            Connection = ValidHttpSource().Connection with
            {
                BaseUrl = "http://lego-train.lan",
                AllowPrivateNetwork = true
            }
        };

        using var response = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-sources",
            source);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that update requires if match.
    /// Description: Arranges an update without a revision precondition, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task UpdateRequiresIfMatch()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var source = ValidHttpSource();
        using var created = await SendYaml(client, HttpMethod.Post, "/api/point-sources", source);

        // Expected outcome: `created.StatusCode` has the required value.
        // Acceptance criteria: `created.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
        // update requires if match.
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var missingHeader = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/point-sources/weather",
            source);

        // Expected outcome: `missingHeader.StatusCode` has the required value.
        // Acceptance criteria: `missingHeader.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
        // update requires if match.
        Assert.That(missingHeader.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that strict yaml and request limit are enforced.
    /// Description: Arranges the inputs for strict yaml and request limit are enforced, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task StrictYamlAndRequestLimitAreEnforced()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        const string duplicate = """
            schemaVersion: 1
            sources:
              - id: weather
                name: Weather
                name: Duplicate
                enabled: true
                kind: http
                connection:
                  baseUrl: https://example.test
                  maximumResponseBytes: 1024
                tls: {verifyServerCertificate: true}
                timeouts: {connectMilliseconds: 100, requestMilliseconds: 100}
            """;
        using var duplicateResponse = await SendRawYaml(client, duplicate);

        // Expected outcome: `duplicateResponse.StatusCode` has the required value.
        // Acceptance criteria: `duplicateResponse.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
        // strict yaml and request limit are enforced.
        Assert.That(duplicateResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var oversized = new string(' ', ConfigurationYaml.MaximumBytes + 1);
        using var oversizedResponse = await SendRawYaml(client, oversized);
        var error =
            await oversizedResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // strict yaml and request limit are enforced.
        Assert.Multiple(() =>
        {
            // Expected outcome: `oversizedResponse.StatusCode` has the required value.
            // Acceptance criteria: `oversizedResponse.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // strict yaml and request limit are enforced.
            Assert.That(oversizedResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Expected outcome: `error!["message"]` has the required value.
            // Acceptance criteria: `error!["message"]` must equal `"unable to read YAML request"`, because this condition proves that
            // strict yaml and request limit are enforced.
            Assert.That(error!["message"], Is.EqualTo("unable to read YAML request"));
        });
    }

    private static PointSource ValidHttpSource() => new()
    {
        Id = "weather",
        Name = "Weather",
        Enabled = true,
        Kind = PointSourceKind.Http,
        Connection = new PointSourceConnection
        {
            BaseUrl = "https://example.test",
            FollowRedirects = false,
            MaximumResponseBytes = 1024
        },
        Tls = new TlsOptions { VerifyServerCertificate = true },
        Timeouts = new PointSourceTimeouts
        {
            ConnectMilliseconds = 100,
            RequestMilliseconds = 100
        }
    };

    private static async Task<HttpResponseMessage> SendYaml(
        HttpClient client,
        HttpMethod method,
        string path,
        PointSource source,
        int? revision = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(
                PointSourceYaml.Render(source),
                Encoding.UTF8,
                "application/yaml")
        };

        if (revision is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", revision.Value.ToString());
        }

        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendRawYaml(HttpClient client, string yaml)
    {
        var content = new StringContent(yaml, Encoding.UTF8, "application/yaml");

        return client.PostAsync("/api/point-sources", content);
    }

    private static async Task<PointSource> ReadSource(HttpResponseMessage response) =>
        PointSourceYaml.Parse(
            Encoding.UTF8.GetBytes(await response.Content.ReadAsStringAsync()));
}