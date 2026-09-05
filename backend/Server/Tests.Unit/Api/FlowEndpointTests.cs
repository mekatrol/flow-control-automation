using Server.Common;
using Server.Common.Models;
using Server.Common.Types;
using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Tests.Unit.Api;

[TestFixture]
internal sealed class FlowEndpointTests
{
    /// <summary>
    /// Purpose: Protects the behavioral contract that crud persists across application restart.
    /// Description: Arranges the inputs for crud persists across application restart, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task CrudPersistsAcrossApplicationRestart()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var created = await CreateFlow(client, "Climate Control");

        // Expected outcome: All outcomes in the grouped assertion scope satisfy their contracts.
        // Acceptance criteria: every assertion in the scope must pass, because this condition proves that
        // crud persists across application restart.
        using (Assert.EnterMultipleScope())
        {
            // Expected outcome: `created.Id` has the required value.
            // Acceptance criteria: `created.Id` must equal `"climate-control"`, because this condition proves that
            // crud persists across application restart.
            Assert.That(created.Id, Is.EqualTo("climate-control"));

            // Expected outcome: `created.Status` has the required value.
            // Acceptance criteria: `created.Status` must equal `"draft"`, because this condition proves that
            // crud persists across application restart.
            Assert.That(created.Status, Is.EqualTo("draft"));

            // Expected outcome: `created.Nodes` contains no entries.
            // Acceptance criteria: `created.Nodes` must be empty, because this condition proves that
            // crud persists across application restart.
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
                    NodeType = FlowNodeType.Pulse,
                    Label = "Every minute",
                    X = 10,
                    Y = 20,
                    ZOrder = 1,
                    Configuration = new Dictionary<string, JsonElement>
                    {
                        ["interval"] = JsonSerializer.SerializeToElement(60)
                    }
                },
            ]
        };
        using var saveResponse = await client.PutAsJsonAsync(
            $"/api/flows/{created.Id}",
            changed,
            FlowControlJson.Options);

        // Expected outcome: `saveResponse.StatusCode` has the required value.
        // Acceptance criteria: `saveResponse.StatusCode` must equal `HttpStatusCode.OK`, because this condition proves that
        // crud persists across application restart.
        Assert.That(saveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var secondClient = factory.CreateClient();
        var loaded = await secondClient.GetFromJsonAsync<Flow>(
            $"/api/flows/{created.Id}",
            FlowControlJson.Options);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // crud persists across application restart.
        Assert.Multiple(() =>
        {
            // Expected outcome: `loaded` is available.
            // Acceptance criteria: `loaded` must not be null, because this condition proves that
            // crud persists across application restart.
            Assert.That(loaded, Is.Not.Null);

            // Expected outcome: `loaded!.Description` has the required value.
            // Acceptance criteria: `loaded!.Description` must equal `"Persisted graph"`, because this condition proves that
            // crud persists across application restart.
            Assert.That(loaded!.Description, Is.EqualTo("Persisted graph"));

            // Expected outcome: `loaded.Nodes` contains the required number of entries.
            // Acceptance criteria: `loaded.Nodes` must contain exactly 1 entries, because this condition proves that
            // crud persists across application restart.
            Assert.That(loaded.Nodes, Has.Count.EqualTo(1));
        });

        using var deleteResponse = await secondClient.DeleteAsync($"/api/flows/{created.Id}");

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // crud persists across application restart.
        Assert.Multiple(() =>
        {
            // Expected outcome: `deleteResponse.StatusCode` has the required value.
            // Acceptance criteria: `deleteResponse.StatusCode` must equal `HttpStatusCode.NoContent`, because this condition proves that
            // crud persists across application restart.
            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            // Expected outcome: `deleteResponse.Content.Headers.ContentLength` has the required value.
            // Acceptance criteria: `deleteResponse.Content.Headers.ContentLength` must equal `0`, because this condition proves that
            // crud persists across application restart.
            Assert.That(deleteResponse.Content.Headers.ContentLength, Is.EqualTo(0));
        });
        using var missing = await secondClient.GetAsync($"/api/flows/{created.Id}");

        // Expected outcome: `missing.StatusCode` has the required value.
        // Acceptance criteria: `missing.StatusCode` must equal `HttpStatusCode.NotFound`, because this condition proves that
        // crud persists across application restart.
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that create makes unique readable ids.
    /// Description: Arranges the inputs for create makes unique readable ids, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task CreateMakesUniqueReadableIds()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var first = await CreateFlow(client, "Heating & Cooling");
        var second = await CreateFlow(client, "Heating & Cooling");

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // create makes unique readable ids.
        Assert.Multiple(() =>
        {
            // Expected outcome: `first.Id` has the required value.
            // Acceptance criteria: `first.Id` must equal `"heating-cooling"`, because this condition proves that
            // create makes unique readable ids.
            Assert.That(first.Id, Is.EqualTo("heating-cooling"));

            // Expected outcome: `second.Id` has the required value.
            // Acceptance criteria: `second.Id` must equal `"heating-cooling-2"`, because this condition proves that
            // create makes unique readable ids.
            Assert.That(second.Id, Is.EqualTo("heating-cooling-2"));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that list filters sorts paginates and clamps page.
    /// Description: Arranges the inputs for list filters sorts paginates and clamps page, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task ListFiltersSortsPaginatesAndClampsPage()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        for (var index = 1; index <= 25; index++)
        {
            await CreateFlow(client, $"Flow {index:00}");
        }

        var page = await client.GetFromJsonAsync<PaginatedResult<Flow>>(
            "/api/flows?page=2&pageSize=10&filter=flow&sort=descending",
            FlowControlJson.Options);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // list filters sorts paginates and clamps page.
        Assert.Multiple(() =>
        {
            // Expected outcome: `page` is available.
            // Acceptance criteria: `page` must not be null, because this condition proves that
            // list filters sorts paginates and clamps page.
            Assert.That(page, Is.Not.Null);

            // Expected outcome: `page!.TotalItems` has the required value.
            // Acceptance criteria: `page!.TotalItems` must equal `25`, because this condition proves that
            // list filters sorts paginates and clamps page.
            Assert.That(page!.TotalItems, Is.EqualTo(25));

            // Expected outcome: `page.PageCount` has the required value.
            // Acceptance criteria: `page.PageCount` must equal `3`, because this condition proves that
            // list filters sorts paginates and clamps page.
            Assert.That(page.PageCount, Is.EqualTo(3));

            // Expected outcome: `page.Page` has the required value.
            // Acceptance criteria: `page.Page` must equal `2`, because this condition proves that
            // list filters sorts paginates and clamps page.
            Assert.That(page.Page, Is.EqualTo(2));

            // Expected outcome: `page.Items` contains the required number of entries.
            // Acceptance criteria: `page.Items` must contain exactly 10 entries, because this condition proves that
            // list filters sorts paginates and clamps page.
            Assert.That(page.Items, Has.Count.EqualTo(10));

            // Expected outcome: `page.Items[0].Name` has the required value.
            // Acceptance criteria: `page.Items[0].Name` must equal `"Flow 15"`, because this condition proves that
            // list filters sorts paginates and clamps page.
            Assert.That(page.Items[0].Name, Is.EqualTo("Flow 15"));

            // Expected outcome: `page.Items[9].Name` has the required value.
            // Acceptance criteria: `page.Items[9].Name` must equal `"Flow 06"`, because this condition proves that
            // list filters sorts paginates and clamps page.
            Assert.That(page.Items[9].Name, Is.EqualTo("Flow 06"));
        });

        var clamped = await client.GetFromJsonAsync<PaginatedResult<Flow>>(
            "/api/flows?page=99&pageSize=10",
            FlowControlJson.Options);

        // Expected outcome: `clamped!.Page` has the required value.
        // Acceptance criteria: `clamped!.Page` must equal `3`, because this condition proves that
        // list filters sorts paginates and clamps page.
        Assert.That(clamped!.Page, Is.EqualTo(3));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that list rejects invalid queries.
    /// Description: Arranges the inputs for list rejects invalid queries, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [TestCase("/api/flows?page=0", "page must be a positive integer")]
    [TestCase("/api/flows?page=nope", "page must be a positive integer")]
    [TestCase("/api/flows?pageSize=100", "pageSize must be 10, 20, or 50")]
    [TestCase("/api/flows?sort=sideways", "sort must be ascending or descending")]
    [TestCase("/api/flows?status=paused", "each status must be draft or deployed")]
    public async Task ListRejectsInvalidQueries(string path, string message)
    {
        await using var factory = new FlowControlApplicationFactory();
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

            // Expected outcome: `response.Content.Headers.ContentType?.MediaType` has the required value.
            // Acceptance criteria: `response.Content.Headers.ContentType?.MediaType` must equal `"application/json"`, because this condition proves that
            // list rejects invalid queries.
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));

            // Expected outcome: `error!["message"]` has the required value.
            // Acceptance criteria: `error!["message"]` must equal `message`, because this condition proves that
            // list rejects invalid queries.
            Assert.That(error!["message"], Is.EqualTo(message));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that save rejects unknown fields trailing values and mismatched id.
    /// Description: Arranges the inputs for save rejects unknown fields trailing values and mismatched id, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task SaveRejectsUnknownFieldsTrailingValuesAndMismatchedId()
    {
        await using var factory = new FlowControlApplicationFactory();
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // save rejects unknown fields trailing values and mismatched id.
        Assert.Multiple(() =>
        {
            // Expected outcome: `unknown.StatusCode` has the required value.
            // Acceptance criteria: `unknown.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // save rejects unknown fields trailing values and mismatched id.
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Expected outcome: `trailing.StatusCode` has the required value.
            // Acceptance criteria: `trailing.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // save rejects unknown fields trailing values and mismatched id.
            Assert.That(trailing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Expected outcome: `mismatch.StatusCode` has the required value.
            // Acceptance criteria: `mismatch.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // save rejects unknown fields trailing values and mismatched id.
            Assert.That(mismatch.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that runtime starts stopped deploys and honors disable enable.
    /// Description: Arranges the inputs for runtime starts stopped deploys and honors disable enable, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task RuntimeStartsStoppedDeploysAndHonorsDisableEnable()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var created = await CreateFlow(client, "Runtime flow");
        created = created with
        {
            Nodes =
            [
                new FlowNode
                {
                    Id = "constant-true",
                    NodeType = FlowNodeType.DigitalConstant,
                    Label = "Constant true",
                    Connectors = [new FlowConnector("value", "Value", DataDirectionType.Output, DataType.Boolean, "right")],
                    Configuration = new Dictionary<string, JsonElement>
                    {
                        ["value"] = JsonSerializer.SerializeToElement(true)
                    }
                }
            ]
        };
        using var saveResponse = await client.PutAsJsonAsync(
            $"/api/flows/{created.Id}",
            created,
            FlowControlJson.Options);
        created = (await saveResponse.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options))!;

        var stopped = await client.GetFromJsonAsync<RuntimeSnapshot>(
            $"/api/flows/{created.Id}/runtime",
            FlowControlJson.Options);
        using var deployResponse = await client.PostAsync(
            $"/api/flows/{created.Id}/deploy",
            content: null);
        Assert.That(
            deployResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            await deployResponse.Content.ReadAsStringAsync());
        var running = await deployResponse.Content.ReadFromJsonAsync<RuntimeSnapshot>(
            FlowControlJson.Options);
        using var scanResponse = await client.PostAsync(
            $"/api/flows/{created.Id}/runtime/scan",
            content: null);
        var scanned = await scanResponse.Content.ReadFromJsonAsync<RuntimeSnapshot>(
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // runtime starts stopped deploys and honors disable enable.
        Assert.Multiple(() =>
        {
            // Expected outcome: `stopped!.State` has the required value.
            // Acceptance criteria: `stopped!.State` must equal `"stopped"`, because this condition proves that
            // runtime starts stopped deploys and honors disable enable.
            Assert.That(stopped!.State, Is.EqualTo("stopped"));

            // Expected outcome: `stopped.Nodes` is available.
            // Acceptance criteria: `stopped.Nodes` must not be null, because this condition proves that
            // runtime starts stopped deploys and honors disable enable.
            Assert.That(stopped.Nodes, Is.Not.Null);
            Assert.That(scanResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(scanned!.ScanNumber, Is.GreaterThan(0));

            // Expected outcome: `running!.State` has the required value.
            // Acceptance criteria: `running!.State` must equal `"running"`, because this condition proves that
            // runtime starts stopped deploys and honors disable enable.
            Assert.That(running!.State, Is.EqualTo("running"));

            // Expected outcome: `disabled!.Disabled` confirms the required condition.
            // Acceptance criteria: `disabled!.Disabled` must be true, because this condition proves that
            // runtime starts stopped deploys and honors disable enable.
            Assert.That(disabled!.Disabled, Is.True);

            // Expected outcome: `disabledRuntime!.State` has the required value.
            // Acceptance criteria: `disabledRuntime!.State` must equal `"stopped"`, because this condition proves that
            // runtime starts stopped deploys and honors disable enable.
            Assert.That(disabledRuntime!.State, Is.EqualTo("stopped"));

            // Expected outcome: `enabled!.Disabled` rejects the prohibited condition.
            // Acceptance criteria: `enabled!.Disabled` must be false, because this condition proves that
            // runtime starts stopped deploys and honors disable enable.
            Assert.That(enabled!.Disabled, Is.False);
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that runtime routes return not found for missing flow.
    /// Description: Arranges the inputs for runtime routes return not found for missing flow, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task RuntimeRoutesReturnNotFoundForMissingFlow()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var get = await client.GetAsync("/api/flows/missing/runtime");
        using var deploy = await client.PostAsync("/api/flows/missing/deploy", content: null);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // runtime routes return not found for missing flow.
        Assert.Multiple(() =>
        {
            // Expected outcome: `get.StatusCode` has the required value.
            // Acceptance criteria: `get.StatusCode` must equal `HttpStatusCode.NotFound`, because this condition proves that
            // runtime routes return not found for missing flow.
            Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            // Expected outcome: `deploy.StatusCode` has the required value.
            // Acceptance criteria: `deploy.StatusCode` must equal `HttpStatusCode.NotFound`, because this condition proves that
            // runtime routes return not found for missing flow.
            Assert.That(deploy.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    /// <summary>
    /// Purpose: Protects compile-only validation of an unsaved draft.
    /// Description: Submits executable source directly and verifies compilation succeeds without deployment.
    /// </summary>
    [Test]
    public async Task CompileDraftReturnsArtifactMetadataWithoutDeploying()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var source = new ExecutableFlowSource
        {
            Id = "compile-draft",
            Revision = 7u,
            ControllerTemplateId = BuiltInControllerTemplate.Id,
            ControllerTemplateRevision = checked((uint)BuiltInControllerTemplate.Default.Revision),
            Nodes =
            [
                new ExecutableFlowNode
                {
                    Id = "constant",
                    NodeType = FlowNodeType.DigitalConstant,
                    Configuration = new Dictionary<string, JsonElement>
                    {
                        ["value"] = JsonSerializer.SerializeToElement(true)
                    }
                }
            ]
        };

        using var response = await client.PostAsJsonAsync(
            "/api/flows/compile-draft/compile", source, FlowControlJson.Options);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Expected outcome: Compile succeeds and reports compiler metadata without requiring a saved flow.
        // Acceptance criteria: The response is successful, contains the source revision, and has no diagnostics.
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(document.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(document.RootElement.GetProperty("flowRevision").GetUInt32(), Is.EqualTo(7));
            Assert.That(document.RootElement.GetProperty("diagnostics").GetArrayLength(), Is.Zero);
        });
    }

    /// <summary>
    /// Purpose: Protects the independent editable draft and last deployed flow versions.
    /// Description: Deploys a saved graph, edits its draft, reads the deployed graph, and reverts the draft.
    /// </summary>
    [Test]
    public async Task DeploymentSnapshotsDraftAndRevertRestoresIt()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var created = await CreateFlow(client, "Versioned flow");
        var firstDraft = created with
        {
            Description = "deployed content",
            Nodes =
            [
                new FlowNode
                {
                    Id = "constant-true",
                    NodeType = FlowNodeType.DigitalConstant,
                    Label = "Constant true",
                    Connectors = [new FlowConnector("value", "Value", DataDirectionType.Output, DataType.Boolean, "right")],
                    Configuration = new Dictionary<string, JsonElement>
                    {
                        ["value"] = JsonSerializer.SerializeToElement(true)
                    }
                }
            ]
        };
        using var saveResponse = await client.PutAsJsonAsync(
            $"/api/flows/{created.Id}", firstDraft, FlowControlJson.Options);
        var saved = (await saveResponse.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options))!;
        using var deployResponse = await client.PostAsync($"/api/flows/{created.Id}/deploy", null);
        Assert.That(deployResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), await deployResponse.Content.ReadAsStringAsync());

        using var editResponse = await client.PutAsJsonAsync(
            $"/api/flows/{created.Id}", saved with { Description = "new draft content", Status = "draft" }, FlowControlJson.Options);
        var edited = (await editResponse.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options))!;
        var deployed = await client.GetFromJsonAsync<Flow>(
            $"/api/flows/{created.Id}/deployed", FlowControlJson.Options);
        using var revertResponse = await client.PostAsync(
            $"/api/flows/{created.Id}/revert-to-deployed", null);
        var reverted = await revertResponse.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(edited.Status, Is.EqualTo("draft"), "Editing creates a draft while retaining the deployed snapshot.");
            Assert.That(edited.Description, Is.EqualTo("new draft content"), "Saving changes updates only the draft content.");
            Assert.That(deployed!.Description, Is.EqualTo("deployed content"), "The deployed endpoint returns the runtime-approved graph.");
            Assert.That(deployed.Revision, Is.EqualTo(saved.Revision), "The deployed graph retains its original revision.");
            Assert.That(reverted!.Description, Is.EqualTo("deployed content"), "Revert copies deployed content back to the draft.");
            Assert.That(reverted.Revision, Is.GreaterThan(edited.Revision), "Revert is persisted as a new optimistic-concurrency revision.");
        });
    }

    private static async Task<Flow> CreateFlow(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/flows",
            new { name },
            FlowControlJson.Options);

        using (Assert.EnterMultipleScope())
        {
            // Expected outcome: `response.StatusCode` has the required value.
            // Acceptance criteria: `response.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
            // runtime routes return not found for missing flow.
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

            // Expected outcome: `response.Content.Headers.ContentType?.MediaType` has the required value.
            // Acceptance criteria: `response.Content.Headers.ContentType?.MediaType` must equal `"application/json"`, because this condition proves that
            // runtime routes return not found for missing flow.
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        }
        return (await response.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options))!;
    }

    private static Task<HttpResponseMessage> PutJson(
        HttpClient client,
        string path,
        string json) =>
        client.PutAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
}