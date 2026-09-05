namespace Server.Common.Models;

public sealed record ExecutableFlowConnection(
    ExecutableFlowEndpoint Source,
    ExecutableFlowEndpoint Target);