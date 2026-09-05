namespace Server.Common.Types;

/// <summary>
/// Specifies the data accepted or produced by a designer connector.
/// </summary>
public enum DataType : byte
{
    /// <summary>
    /// Accepts a value without restricting its data type.
    /// </summary>
    Any = 0,

    /// <summary>
    /// Carries a logical true or false value.
    /// </summary>
    Boolean,

    /// <summary>
    /// Carries a numeric value for arithmetic or measurement.
    /// </summary>
    Number,

    /// <summary>
    /// Carries a text value.
    /// </summary>
    String,

    /// <summary>
    /// Carries an event notification rather than a continuous value.
    /// </summary>
    Event
}