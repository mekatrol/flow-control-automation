using Server.Common.Types;
using System.Text.Json;

namespace Server.Common.Models;

/// <summary>
/// Defines an execution-scoped virtual point required by one or more flows, including the
/// point's data shape, permitted operations, persistence behaviour, and fallback value.
/// The runtime uses this definition to allocate and manage the virtual point within an
/// execution instance.
/// </summary>
public sealed record VirtualPointDefinition
{
    /// <summary>Gets the name that identifies the virtual point within an execution instance.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the type of value held by the virtual point.</summary>
    public required AutomationPointValueType ValueType { get; init; }

    /// <summary>Gets the engineering units for an analog virtual point.</summary>
    public string? Units { get; init; }

    /// <summary>Gets whether flows can read the virtual point's value.</summary>
    public bool Readable { get; init; }

    /// <summary>Gets whether flows can command the virtual point's value.</summary>
    public bool Commandable { get; init; }

    /// <summary>Gets how the virtual point's value is preserved across runtime lifecycles.</summary>
    public VirtualPointPersistenceType Persistence { get; init; }

    /// <summary>Gets the value used when no commanded value is available.</summary>
    public JsonElement? RelinquishDefault { get; init; }
}