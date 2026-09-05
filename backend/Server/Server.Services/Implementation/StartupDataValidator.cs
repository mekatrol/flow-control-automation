using Server.Common.Contracts;

namespace Server.Services.Implementation;

internal sealed class StartupDataValidator(
    IFlowService flows,
    IPointSourceService pointSources,
    IPointSourceValidator pointSourceValidator,
    IPointDefinitionStore pointDefinitions,
    IPointDefinitionValidator pointDefinitionValidator,
    IControllerTemplateStore controllerTemplates,
    ICredentialStore credentials,
    ICredentialResolver credentialResolver) : IStartupDataValidator
{
    public async Task ValidateAsync(CancellationToken cancellationToken)
    {
        // Listing materializes every stored row before pagination, detecting
        // malformed JSON before the server accepts traffic.
        await flows.ListAsync(
            new FlowListOptions(PageSize: 50),
            cancellationToken);

        var firstPage = await pointSources.ListAsync(
            new PointSourceListOptions(PageSize: 50),
            cancellationToken);
        ValidateSources(firstPage.Items);
        for (var page = 2; page <= firstPage.PageCount; page++)
        {
            var nextPage = await pointSources.ListAsync(
                new PointSourceListOptions(Page: page, PageSize: 50),
                cancellationToken);
            ValidateSources(nextPage.Items);
        }

        var sources = new Dictionary<string, PointSource>(StringComparer.Ordinal);
        for (var page = 1; page <= firstPage.PageCount; page++)
        {
            var sourcePage = page == 1
                ? firstPage
                : await pointSources.ListAsync(
                    new PointSourceListOptions(Page: page, PageSize: 50),
                    cancellationToken);
            foreach (var source in sourcePage.Items)
            {
                sources.Add(source.Id, source);
            }
        }

        var groups = await pointDefinitions.ListGroupsAsync(cancellationToken);
        pointDefinitionValidator.ValidateDocument(
            new PointDocument
            {
                Groups = groups,
                Points = await pointDefinitions.ListPointsAsync(cancellationToken)
            },
            sources);

        await controllerTemplates.ListAsync(cancellationToken);

        foreach (var credential in await credentials.ListAsync(cancellationToken))
        {
            // Decrypt every row at startup so a wrong key or damaged ciphertext
            // cannot remain latent until a connectivity request.
            await credentialResolver.ResolveAsync(
                $"secret://{credential.Id}",
                cancellationToken);
        }
    }

    private void ValidateSources(IReadOnlyList<PointSource> sources)
    {
        foreach (var source in sources)
        {
            pointSourceValidator.Validate(source);
        }
    }
}