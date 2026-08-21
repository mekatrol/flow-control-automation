using System.Text.Json;

namespace Server.Common.Contracts;

public sealed record VirtualPointDeclaration
{
    public required string Key { get; init; }
    public required FlowPointValueType ValueType { get; init; }
    public string? Units { get; init; }
    public bool Readable { get; init; }
    public bool Commandable { get; init; }
    public VirtualPointPersistence Persistence { get; init; }
    public JsonElement? RelinquishDefault { get; init; }
}
