using Server.Services.Contracts;

namespace Server.Services.Implementation;

internal sealed class StartupDataValidator(
    IFlowService flows,
    IPointSourceService pointSources,
    IPointSourceValidator pointSourceValidator,
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