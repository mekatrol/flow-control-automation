using Server.Data.Context;
using Server.Data.Entities;
using Server.Services;
using Server.Services.Contracts;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tests.Unit.Api;

[TestFixture]
internal sealed class StartupValidationTests
{
    /// <summary>
    /// Purpose: Protects the behavioral contract that validator rejects malformed stored flow.
    /// Description: Arranges the inputs for validator rejects malformed stored flow, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task ValidatorRejectsMalformedStoredFlow()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            context.Flows.Add(new FlowEntity
            {
                Id = "damaged",
                Key = "damaged",
                Json = "{not-json",
                Created = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await using var validationScope = factory.Services.CreateAsyncScope();
        var validator =
            validationScope.ServiceProvider.GetRequiredService<IStartupDataValidator>();

        // Expected outcome: The invalid operation is rejected with the required error.
        // Acceptance criteria: the operation must throw JsonException, because this condition proves that
        // validator rejects malformed stored flow.
        Assert.ThrowsAsync<JsonException>(
            async () => await validator.ValidateAsync(CancellationToken.None));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that validator rejects undecryptable stored credential.
    /// Description: Arranges the inputs for validator rejects undecryptable stored credential, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task ValidatorRejectsUndecryptableStoredCredential()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ICredentialStore>();
            await store.CreateAsync(
                new CredentialInput
                {
                    Id = "damaged",
                    Name = "Damaged",
                    Kind = "token",
                    Token = "temporary-test-material"
                },
                CancellationToken.None);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            var row = await context.Credentials.FindAsync(["damaged"], CancellationToken.None);
            var document = JsonNode.Parse(row!.Json)!.AsObject();
            document["secret"] = "not-valid-ciphertext";
            row.Json = document.ToJsonString(FlowControlJson.Options);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await using var validationScope = factory.Services.CreateAsyncScope();
        var validator =
            validationScope.ServiceProvider.GetRequiredService<IStartupDataValidator>();

        // Expected outcome: The invalid operation is rejected with the required error.
        // Acceptance criteria: the operation must throw CredentialResolutionException, because this condition proves that
        // validator rejects undecryptable stored credential.
        Assert.ThrowsAsync<CredentialResolutionException>(
            async () => await validator.ValidateAsync(CancellationToken.None));
    }
}