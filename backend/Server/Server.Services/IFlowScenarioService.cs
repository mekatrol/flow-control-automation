using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowScenarioService
{
    Task<IReadOnlyList<FlowScenario>> ListAsync(string flowId, CancellationToken cancellationToken);
    Task<FlowScenario> GetAsync(string flowId, string scenarioId, CancellationToken cancellationToken);
    Task<FlowScenario> SaveAsync(FlowScenario scenario, CancellationToken cancellationToken);
    Task DeleteAsync(string flowId, string scenarioId, CancellationToken cancellationToken);
    Task<FlowScenarioRunResult> RunAsync(FlowScenario scenario, ExecutableFlowSource source, CancellationToken cancellationToken);
}