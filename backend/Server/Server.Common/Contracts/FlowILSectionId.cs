namespace Server.Common.Contracts;

/// <summary>
/// Identifies the Flow IL section stored in the artifact directory.
/// Values correspond directly to the section IDs encoded in the Flow IL v1 binary format.
/// </summary>
public enum FlowILSectionId : int
{
    /// <summary>
    /// Contains the canonical typed constant pool used by instructions.
    /// </summary>
    Constants = 1,

    /// <summary>
    /// Contains controller-point and flow-interface binding records.
    /// </summary>
    Points = 2,

    /// <summary>
    /// Contains transient and persistent state-slot definitions.
    /// </summary>
    Slots = 3,

    /// <summary>
    /// Contains the executable Flow VM instruction stream.
    /// </summary>
    Instructions = 4,

    /// <summary>
    /// Contains state-commit operations performed at the end of a scan.
    /// </summary>
    CommitPlan = 5,

    /// <summary>
    /// Contains source-node identity and designer metadata used for decompilation
    /// and authoring recovery.
    /// </summary>
    Symbols = 6,

    /// <summary>
    /// Maps executable instructions back to source nodes for debugging.
    /// </summary>
    DebugMap = 7,

    /// <summary>
    /// Contains controller-template and controller-point revision dependencies.
    /// </summary>
    Dependencies = 8
}