using Microsoft.Extensions.DependencyInjection;
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
    [Test]
    public async Task ValidatorRejectsMalformedStoredFlow()
    {
        using var factory = new FlowControlApplicationFactory();
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
                Updated = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await using var validationScope = factory.Services.CreateAsyncScope();
        var validator =
            validationScope.ServiceProvider.GetRequiredService<IStartupDataValidator>();
        Assert.ThrowsAsync<JsonException>(
            async () => await validator.ValidateAsync(CancellationToken.None));
    }

    [Test]
    public async Task ValidatorRejectsUndecryptableStoredCredential()
    {
        using var factory = new FlowControlApplicationFactory();
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
                    Token = "temporary-test-material",
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
        Assert.ThrowsAsync<CredentialResolutionException>(
            async () => await validator.ValidateAsync(CancellationToken.None));
    }
}