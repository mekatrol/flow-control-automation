using Server.Services.Contracts;

namespace Server.Api.Contracts;

/// <summary>Requests an isolated simulator session for an executable flow snapshot.</summary>
/// <param name="Source">The complete, validated flow snapshot to compile and simulate; it must identify a supported current contract version.</param>
/// <param name="ReplaceExisting">Whether an existing session for the same flow may be stopped and atomically replaced; otherwise a conflict is returned.</param>
public sealed record CreateSimulatorSessionRequest(ExecutableFlowSource Source, bool ReplaceExisting = true);