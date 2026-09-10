namespace Server.Common.Models.Communication;

public sealed record ControllerProtocolCapabilities(
    ushort MaximumFrameBytes,
    ushort MaximumPayloadBytes,
    IReadOnlySet<byte> Operations,
    IReadOnlySet<ushort> ArtifactSchemas);