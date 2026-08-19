namespace Server.Compiler.Contracts;

/// <summary>
/// Identifies the record type used by an Intel HEX record.
/// Values are defined by the Intel HEX file format.
/// </summary>
internal enum IntelHexRecordType : byte
{
    /// <summary>
    /// Contains executable or data bytes at the specified address.
    /// </summary>
    Data = 0x00,

    /// <summary>
    /// Marks the end of the Intel HEX file.
    /// </summary>
    EndOfFile = 0x01,

    /// <summary>
    /// Defines the upper 16 bits of the address used by subsequent data records.
    /// </summary>
    ExtendedLinearAddress = 0x04
}