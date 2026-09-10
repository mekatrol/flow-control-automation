namespace Server.Common.Models.FlowExecution;

public sealed record StartFlowDebugSession(
    ExecutableFlowSource Source,
    string Host,
    bool ReplaceExisting,
    string? EmulatorId = null);