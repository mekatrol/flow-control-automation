using Server.Services.Contracts;

namespace Server.Api.Contracts;

/// <summary>Combines a scenario with the immutable flow snapshot against which it will run.</summary>
/// <param name="Scenario">The ordered inputs and expectations to execute; it must satisfy scenario validation and may not be <see langword="null"/>.</param>
/// <param name="Source">The complete executable flow snapshot referenced by the scenario; it must use the current supported contract version.</param>
public sealed record RunFlowScenarioRequest(FlowScenario Scenario, ExecutableFlowSource Source);