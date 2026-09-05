namespace Server.Common.Types;

/// <summary>
/// Specifies the value representation of an automation point.
/// </summary>
public enum AutomationPointValueType : byte
{
    /// <summary>
    /// Represents a numeric measurement or continuously varying value.
    /// </summary>
    Analog = 1,

    /// <summary>
    /// Represents a logical true or false value.
    /// </summary>
    Digital,

    /// <summary>
    /// Represents one of a defined set of discrete states.
    /// </summary>
    MultiState,

    /// <summary>
    /// Represents a whole-number value.
    /// </summary>
    Integer,

    /// <summary>
    /// Represents a text value.
    /// </summary>
    Text
}