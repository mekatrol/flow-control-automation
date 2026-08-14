using Server.Services.Contracts;

namespace Server.Api.Contracts;

public sealed record CreateSimulatorSessionRequest(ExecutableFlowSource Source, bool ReplaceExisting = true);
