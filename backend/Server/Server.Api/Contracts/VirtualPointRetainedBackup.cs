using Server.Services.Contracts;

namespace Server.Api.Contracts;

public sealed record VirtualPointRetainedBackup
{
    public int SchemaVersion { get; init; }
    public required string ExecutionInstanceId { get; init; }
    public IReadOnlyDictionary<string, RetainedVirtualPointValue> Values { get; init; } = new Dictionary<string, RetainedVirtualPointValue>();
}