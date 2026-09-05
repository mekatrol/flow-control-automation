namespace Server.Common.Types;

/// <summary>
/// Identifies the storage purpose of a slot in a Flow IL artifact.
/// </summary>
public enum FlowSlotType : byte
{
    /// <summary>
    /// Stores a transient value used during flow execution.
    /// </summary>
    Value = 2,

    /// <summary>
    /// Stores a memory node value across scans.
    /// </summary>
    MemoryState = 3,

    /// <summary>
    /// Stores persistent state for time-dependent operations.
    /// </summary>
    TimerState = 4,

    /// <summary>
    /// Stores previous input state for edge detection.
    /// </summary>
    EdgeState = 5,

    /// <summary>
    /// Stores persistent state for a counter operation.
    /// </summary>
    CounterState = 6
}