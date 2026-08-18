namespace Server.Services.Contracts;

/// <summary>
/// Capability flags encoded in a Flow IL v1 artifact.
/// Multiple capabilities may be combined into a single capability mask.
/// </summary>
[Flags]
internal enum FlowILCapability : ulong
{
    None = 0,

    /// <summary>
    /// Indicates support for the base Flow IL execution profile.
    /// </summary>
    Base = 1UL << 0,

    /// <summary>
    /// Indicates that the compiled flow reads one or more external inputs.
    /// </summary>
    Inputs = 1UL << 1,

    /// <summary>
    /// Indicates that the compiled flow writes one or more external outputs.
    /// </summary>
    Outputs = 1UL << 2,

    /// <summary>
    /// Indicates that the compiled flow requires persistent state storage.
    /// </summary>
    State = 1UL << 3,

    /// <summary>
    /// Indicates support for the core Boolean instruction set.
    /// </summary>
    Boolean = 1UL << 4,

    /// <summary>
    /// Indicates support for the expanded Boolean instruction set.
    /// </summary>
    ExpandedBoolean = 1UL << 5,

    /// <summary>
    /// Indicates support for numeric operations and numeric values.
    /// </summary>
    Numeric = 1UL << 6,

    /// <summary>
    /// Indicates support for comparison operations.
    /// </summary>
    Comparison = 1UL << 7,

    /// <summary>
    /// Indicates support for level-shifter operations.
    /// </summary>
    LevelShifter = 1UL << 8,

    /// <summary>
    /// Indicates support for quality-aware operations and execution.
    /// </summary>
    Quality = 1UL << 9,

    /// <summary>
    /// Indicates support for timer and time-dependent operations.
    /// </summary>
    Timer = 1UL << 10,

    /// <summary>
    /// Indicates support for event-related operations.
    /// </summary>
    Event = 1UL << 11
}