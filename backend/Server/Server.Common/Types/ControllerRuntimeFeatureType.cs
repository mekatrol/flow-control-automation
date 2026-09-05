namespace Server.Common.Types;

/// <summary>
/// Identifies a runtime capability advertised by a controller template.
/// </summary>
public enum ControllerRuntimeFeatureType : byte
{
    /// <summary>
    /// Supports software points that are not directly mapped to physical hardware.
    /// </summary>
    VirtualPoints,

    /// <summary>
    /// Supports binding flow points to controller points.
    /// </summary>
    BoundPoints,

    /// <summary>
    /// Supports resolving competing commands for a point.
    /// </summary>
    CommandArbitration,

    /// <summary>
    /// Supports propagating data quality through flow execution.
    /// </summary>
    QualityPropagation
}