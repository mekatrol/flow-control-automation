namespace Server.Services.Contracts;

public sealed record ExecutableFlowConnection(
    ExecutableFlowEndpoint Source,
    ExecutableFlowEndpoint Target);