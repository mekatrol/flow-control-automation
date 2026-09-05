using Server.Common.Types;
using System.Text.Json;

namespace Server.Common.Models;

public sealed record VirtualPointDeclaration
{
    public required string Key { get; init; }
    public required AutomationPointValueType ValueType { get; init; }
    public string? Units { get; init; }
    public bool Readable { get; init; }
    public bool Commandable { get; init; }
    public VirtualPointPersistenceType Persistence { get; init; }
    public JsonElement? RelinquishDefault { get; init; }
}