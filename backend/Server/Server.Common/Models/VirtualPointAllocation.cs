namespace Server.Common.Models;

public sealed record VirtualPointAllocation(
    string ExecutionInstanceId,
    string PointKey,
    VirtualPointDeclaration ResolvedContract);