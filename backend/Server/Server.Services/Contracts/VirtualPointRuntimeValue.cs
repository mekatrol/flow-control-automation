using Server.Common.Models;
using Server.Common.Types;

namespace Server.Services.Contracts;

public sealed record VirtualPointRuntimeValue
{
    public required string ExecutionInstanceId { get; init; }
    public required string PointKey { get; init; }
    public required VirtualPointDefinition Contract { get; init; }
    public FlowVmValue? Value { get; init; }
    public DataQualityType Quality { get; init; } = DataQualityType.Unavailable;
    public string? Timestamp { get; init; }
    public string? WriterFlowId { get; init; }
    public IReadOnlyList<string> ReaderFlowIds { get; init; } = [];
    public bool Retained { get; init; }
    public ulong Version { get; init; }
}