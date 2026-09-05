namespace Server.Common.Types;

/// <summary>
/// Identifies a connector data type supported by a controller template.
/// </summary>
public enum ConnectorDataType : byte
{
    /// <summary>
    /// Supports connectors that do not restrict the value type.
    /// </summary>
    Any,

    /// <summary>
    /// Supports connectors carrying logical true or false values.
    /// </summary>
    Boolean,

    /// <summary>
    /// Supports connectors carrying event notifications.
    /// </summary>
    Event,

    /// <summary>
    /// Supports connectors carrying numeric values.
    /// </summary>
    Number,

    /// <summary>
    /// Supports connectors carrying text values.
    /// </summary>
    String
}