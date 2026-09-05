namespace Server.Common.Types;

/// <summary>
/// Specifies the source from which a point obtains or exposes its value.
/// </summary>
public enum PointSourceType
{
    /// <summary>
    /// The point is mapped to physical input or output hardware on a device,
    /// such as a digital input, digital output, analog input, analog output,
    /// relay, or other physical I/O.
    /// </summary>
    Physical,

    /// <summary>
    /// The point represents an internal software or memory-based value that
    /// is not directly mapped to physical I/O or an external data source.
    /// </summary>
    Virtual,

    /// <summary>
    /// The point is mapped to a value provided or consumed through an external
    /// communication interface or protocol, such as MQTT, HTTP,
    /// RS-485, CAN, or another remote device or service.
    /// </summary>
    Remote
}