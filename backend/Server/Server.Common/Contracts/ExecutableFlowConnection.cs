namespace Server.Common.Contracts;

public sealed record ExecutableFlowConnection(
    ExecutableFlowEndpoint Source,
    ExecutableFlowEndpoint Target);