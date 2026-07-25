using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowService
{
    Task<PaginatedResult<Flow>> ListAsync(
        FlowListOptions options,
        CancellationToken cancellationToken);

    Task<Flow> GetAsync(string id, CancellationToken cancellationToken);

    Task<Flow> CreateAsync(string name, CancellationToken cancellationToken);

    Task<Flow> SaveAsync(string id, Flow flow, CancellationToken cancellationToken);

    Task<Flow> SetDisabledAsync(
        string id,
        bool disabled,
        CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}