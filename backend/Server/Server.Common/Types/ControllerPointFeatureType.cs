namespace Server.Common.Types;

/// <summary>
/// Identifies a point capability advertised by a controller template.
/// </summary>
public enum ControllerPointFeatureType : byte
{
    /// <summary>
    /// Allows the current point value to be read.
    /// </summary>
    Read,

    /// <summary>
    /// Allows commands to be issued to a point.
    /// </summary>
    Command,

    /// <summary>
    /// Allows point values to be retained across runtime restarts.
    /// </summary>
    Retain,

    /// <summary>
    /// Allows a point value to be overridden.
    /// </summary>
    Override,

    /// <summary>
    /// Allows an active point command to be released.
    /// </summary>
    Relinquish,

    /// <summary>
    /// Provides quality information alongside point values.
    /// </summary>
    Quality,

    /// <summary>
    /// Supports alarm handling for points.
    /// </summary>
    Alarms,

    /// <summary>
    /// Supports recording point values over time.
    /// </summary>
    Trends
}