namespace Server.Services;

public interface IPointSourceService
{
    Task<PaginatedResult<PointSource>> ListAsync(
        PointSourceListOptions options,
        CancellationToken cancellationToken);

    Task<PointSource> GetAsync(string id, CancellationToken cancellationToken);

    Task<PointSource> CreateAsync(
        PointSource source,
        CancellationToken cancellationToken);

    Task<PointSource> UpdateAsync(
        string id,
        PointSource source,
        int revision,
        CancellationToken cancellationToken);

    Task DeleteAsync(string id, int revision, CancellationToken cancellationToken);
}