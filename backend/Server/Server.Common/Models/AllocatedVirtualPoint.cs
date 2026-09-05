namespace Server.Common.Models;

public sealed record AllocatedVirtualPoint(
    string ExecutionInstanceId,
    VirtualPointDefinition Definition);