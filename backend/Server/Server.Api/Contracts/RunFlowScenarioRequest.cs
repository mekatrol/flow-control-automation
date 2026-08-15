using Server.Services.Contracts;

namespace Server.Api.Contracts;

public sealed record RunFlowScenarioRequest(FlowScenario Scenario, ExecutableFlowSource Source);