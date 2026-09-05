namespace Server.Common.Types;

/// <summary>
/// Specifies how data moves through a point or connector.
/// </summary>
public enum DataDirectionType : byte
{
    /// <summary>
    /// Receives data from a source, such as a sensor or an upstream flow node.
    /// </summary>
    Input = 1,

    /// <summary>
    /// Exposes or writes data to a destination, such as an actuator or a downstream flow node.
    /// </summary>
    Output,

    /// <summary>
    /// Supports both receiving and writing data through the same point or connector.
    /// </summary>
    InputOutput,

    /// <summary>
    /// Represents a value without an input or output direction.
    /// </summary>
    Value
}