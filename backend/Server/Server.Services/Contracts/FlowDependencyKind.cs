namespace Server.Services.Contracts;

/// <summary>
/// Identifies the type of external dependency recorded in the Flow IL dependency section.
/// Values correspond directly to the encoded dependency-kind byte.
/// </summary>
internal enum FlowDependencyKind : byte
{
    /// <summary>
    /// Identifies the controller template against which the flow was compiled.
    /// </summary>
    ControllerTemplate = 1,

    /// <summary>
    /// Identifies a physical controller point referenced by the compiled flow.
    /// </summary>
    ControllerPoint = 2
}
