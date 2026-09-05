using Server.Common.Models;

namespace Server.Services.Contracts;

public sealed record StartFlowDebugSession(
    ExecutableFlowSource Source,
    string Host,
    bool ReplaceExisting,
    string? EmulatorId = null);