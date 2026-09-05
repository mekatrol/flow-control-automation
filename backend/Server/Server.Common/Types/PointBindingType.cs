namespace Server.Common.Types;

/// <summary>
/// Identifies how a compiled point reference is resolved at runtime.
/// </summary>
public enum PointBindingType : byte
{
    /// <summary>
    /// Resolves the reference to a point supplied by the controller.
    /// </summary>
    ControllerPoint = 0,

    /// <summary>
    /// Resolves the reference to a software point allocated by the runtime.
    /// </summary>
    VirtualPoint = 1
}