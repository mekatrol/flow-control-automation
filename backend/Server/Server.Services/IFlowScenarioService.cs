using Server.Services.Contracts;

namespace Server.Services;

/// <summary>Stores and executes deterministic test scenarios associated with flows.</summary>
public interface IFlowScenarioService
{
    /// <summary>Lists every saved scenario for one flow in deterministic storage order.</summary>
    /// <param name="flowId">The non-empty flow ID whose scenarios are requested.</param>
    /// <param name="cancellationToken">Cancels the read without changing stored scenarios.</param>
    /// <returns>A possibly empty immutable scenario list.</returns>
    Task<IReadOnlyList<FlowScenario>> ListAsync(string flowId, CancellationToken cancellationToken);

    /// <summary>Gets one scenario belonging to a flow.</summary>
    /// <param name="flowId">The non-empty owning flow ID.</param>
    /// <param name="scenarioId">The non-empty scenario ID unique within the flow.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The current stored scenario.</returns>
    Task<FlowScenario> GetAsync(string flowId, string scenarioId, CancellationToken cancellationToken);

    /// <summary>Creates or replaces a validated scenario according to its identity and revision contract.</summary>
    /// <param name="scenario">The complete current-schema scenario with ordered steps and expectations.</param>
    /// <param name="cancellationToken">Cancels before the atomic save commits.</param>
    /// <returns>The persisted scenario including its authoritative revision.</returns>
    Task<FlowScenario> SaveAsync(FlowScenario scenario, CancellationToken cancellationToken);

    /// <summary>Deletes one scenario without affecting its owning flow.</summary>
    /// <param name="flowId">The non-empty owning flow ID.</param>
    /// <param name="scenarioId">The non-empty scenario ID to delete.</param>
    /// <param name="cancellationToken">Cancels before deletion commits.</param>
    /// <returns>A task that completes when the scenario is no longer stored.</returns>
    Task DeleteAsync(string flowId, string scenarioId, CancellationToken cancellationToken);

    /// <summary>Runs a scenario against a newly compiled isolated VM without changing deployment state.</summary>
    /// <param name="scenario">The validated scenario to execute from its initial deterministic state.</param>
    /// <param name="source">The immutable executable source whose flow ID must match the scenario owner.</param>
    /// <param name="cancellationToken">Cancels compilation or execution before further steps run.</param>
    /// <returns>Ordered expectation results and final scenario success state.</returns>
    Task<FlowScenarioRunResult> RunAsync(FlowScenario scenario, ExecutableFlowSource source, CancellationToken cancellationToken);
}