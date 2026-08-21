using Server.Common.Contracts;

namespace Server.Services.Contracts;

public sealed record VirtualPointRuntimeValue
{
    public required string ExecutionInstanceId { get; init; }
    public required string PointKey { get; init; }
    public required VirtualPointDeclaration Contract { get; init; }
    public FlowVmValue? Value { get; init; }
    public DataQuality Quality { get; init; } = DataQuality.Unavailable;
    public string? Timestamp { get; init; }
    public string? WriterFlowId { get; init; }
    public ulong Version { get; init; }
}
