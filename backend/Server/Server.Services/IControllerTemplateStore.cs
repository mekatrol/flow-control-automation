using Server.Services.Contracts;

namespace Server.Services;

public interface IControllerTemplateStore
{
    Task<IReadOnlyList<ControllerTemplate>> ListAsync(CancellationToken cancellationToken);
    Task<ControllerTemplate> GetAsync(string id, CancellationToken cancellationToken);
    Task<ControllerTemplate> CreateAsync(
        ControllerTemplate template,
        CancellationToken cancellationToken);
    Task<ControllerTemplate> UpdateAsync(
        string id,
        ControllerTemplate template,
        int revision,
        CancellationToken cancellationToken);
    Task DeleteAsync(string id, int revision, CancellationToken cancellationToken);
}