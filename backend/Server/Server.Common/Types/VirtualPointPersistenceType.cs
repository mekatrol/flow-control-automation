namespace Server.Common.Types;

/// <summary>
/// Specifies whether a virtual point value survives a runtime restart.
/// </summary>
public enum VirtualPointPersistenceType
{
    /// <summary>
    /// The value is held in memory and is not retained across restarts.
    /// </summary>
    Volatile,

    /// <summary>
    /// The value is retained for restoration after a runtime restart.
    /// </summary>
    Retained
}