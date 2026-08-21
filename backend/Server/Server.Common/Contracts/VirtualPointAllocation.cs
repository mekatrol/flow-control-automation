namespace Server.Common.Contracts;

public sealed record VirtualPointAllocation(
    string ExecutionInstanceId,
    string PointKey,
    VirtualPointDeclaration ResolvedContract);
