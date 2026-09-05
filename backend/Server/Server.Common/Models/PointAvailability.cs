using Server.Common.Types;

namespace Server.Common.Models;

/// <summary>
/// Describes whether a point is available for use within an execution context or instance
/// and, when available, exposes the point characteristics needed to configure a flow.
/// The available point may be defined by the execution context or supplied by the shared
/// automation-point catalogue.
/// </summary>
public sealed record PointAvailability
{
    /// <summary>Gets the execution context in which availability was evaluated.</summary>
    public string? ExecutionContextId { get; init; }

    /// <summary>Gets the execution instance in which availability was evaluated.</summary>
    public string? ExecutionInstanceId { get; init; }

    /// <summary>Gets the key of the point whose availability was evaluated.</summary>
    public required string PointKey { get; init; }

    /// <summary>Gets whether the point is available in the requested scope.</summary>
    public bool Exists { get; init; }

    /// <summary>Gets the source of the available point.</summary>
    public PointSourceType? PointSourceType { get; init; }

    /// <summary>Gets the type of value exposed by the available point.</summary>
    public AutomationPointValueType? ValueType { get; init; }

    /// <summary>Gets whether the available point can be read.</summary>
    public bool Readable { get; init; }

    /// <summary>Gets whether the available point can be commanded.</summary>
    public bool Commandable { get; init; }

    /// <summary>Gets the engineering units associated with the available point.</summary>
    public string? Units { get; init; }

    /// <summary>Gets the revision of the definition from which availability was determined.</summary>
    public int Revision { get; init; }

    /// <summary>Gets whether the available point is enabled for use.</summary>
    public bool Enabled { get; init; }
}