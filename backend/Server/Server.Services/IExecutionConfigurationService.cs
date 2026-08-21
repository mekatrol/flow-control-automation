using Server.Common.Contracts;

namespace Server.Services;

public interface IExecutionConfigurationService
{
    Task<IReadOnlyList<ExecutionContextDefinition>> ListContextsAsync(CancellationToken cancellationToken);
    Task<ExecutionContextDefinition> GetContextAsync(string id, CancellationToken cancellationToken);
    Task<ExecutionContextDefinition> SaveContextAsync(ExecutionContextDefinition definition, bool create, CancellationToken cancellationToken);
    Task DeleteContextAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionInstance>> ListInstancesAsync(CancellationToken cancellationToken);
    Task<ExecutionInstance> GetInstanceAsync(string id, CancellationToken cancellationToken);
    Task<ExecutionInstance> SaveInstanceAsync(ExecutionInstance instance, bool create, CancellationToken cancellationToken);
    Task DeleteInstanceAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionContextDeployment>> ListDeploymentsAsync(string contextId, CancellationToken cancellationToken);
    Task<ExecutionContextDeployment> SaveDeploymentAsync(ExecutionContextDeployment deployment, bool create, CancellationToken cancellationToken);
    Task DeleteDeploymentAsync(string contextId, string deploymentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VirtualPointAllocation>> ListAllocationsAsync(string instanceId, CancellationToken cancellationToken);
}