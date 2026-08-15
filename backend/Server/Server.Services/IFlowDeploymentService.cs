using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowDeploymentService
{
    Task<RuntimeSnapshot> DeployAsync(Flow flow, CancellationToken cancellationToken);
}