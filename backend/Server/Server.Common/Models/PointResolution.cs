using Server.Common.Types;

namespace Server.Common.Models;

public sealed record PointResolution
{
    public string? ExecutionContextId { get; init; }
    public string? ExecutionInstanceId { get; init; }
    public required string PointKey { get; init; }
    public bool Exists { get; init; }
    public PointSourceType? PointSourceType { get; init; }
    public AutomationPointValueType? ValueType { get; init; }
    public bool Readable { get; init; }
    public bool Commandable { get; init; }
    public string? Units { get; init; }
    public int Revision { get; init; }
    public bool Enabled { get; init; }
}