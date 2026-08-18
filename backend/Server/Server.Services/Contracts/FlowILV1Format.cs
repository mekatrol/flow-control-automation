namespace Server.Services.Contracts;

internal static class FlowILV1Format
{
    /// <summary>
    /// Flow IL binary format version.
    /// </summary>
    public const ushort Version = 1;

    /// <summary>
    /// Number of sections required by a canonical Flow IL v1 artifact.
    /// </summary>
    public const ushort SectionCount = 8;

    /// <summary>
    /// Size in bytes of the fixed Flow IL envelope.
    /// </summary>
    public const int EnvelopeLength = 128;

    /// <summary>
    /// Size in bytes of one section-directory entry.
    /// </summary>
    public const int DirectoryEntryLength = 48;

    /// <summary>
    /// Maximum permitted size, in bytes, of a complete Flow IL v1 artifact.
    /// Artifacts exceeding this limit are rejected by the compiler and decompiler.
    /// </summary>
    public const int MaximumArtifactBytes = 16384;

    /// <summary>
    /// Reserved byte value. Reserved fields must be encoded as zero.
    /// </summary>
    public const byte ReservedByte = 0;

    /// <summary>
    /// Value required for reserved 16-bit fields in the Flow IL v1 format.
    /// </summary>
    public const ushort ReservedUInt16 = 0;

    /// <summary>
    /// Sentinel used when an instruction operand or slot reference is unused.
    /// </summary>
    public const ushort Unused = ushort.MaxValue;
}
