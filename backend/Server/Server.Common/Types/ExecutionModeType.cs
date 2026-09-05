namespace Server.Common.Types;

/// <summary>
/// Identifies an execution trigger supported by a controller template.
/// </summary>
public enum ExecutionModeType : byte
{
    /// <summary>
    /// Triggers execution in response to an event.
    /// </summary>
    Event,

    /// <summary>
    /// Triggers execution repeatedly at a configured interval.
    /// </summary>
    Interval
}