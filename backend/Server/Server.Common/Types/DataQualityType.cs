namespace Server.Common.Types;

/// <summary>
/// Describes the reliability and availability of a runtime data value.
/// </summary>
public enum DataQualityType : byte
{
    /// <summary>
    /// The value is available and considered reliable for execution.
    /// </summary>
    Good = 1,

    /// <summary>
    /// The value is present but is not considered reliable for execution.
    /// </summary>
    Bad,

    /// <summary>
    /// The reliability of the value cannot be established.
    /// </summary>
    Uncertain,

    /// <summary>
    /// No usable value is currently available from the source.
    /// </summary>
    Unavailable
}