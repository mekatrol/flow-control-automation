namespace Server.Common.Models.Communication;

public sealed record HttpJsonPointMapping(
    string Path,
    string Method,
    string? JsonPointer) : PointMapping;