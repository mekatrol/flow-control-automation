namespace Server.Common.Models.Communication;

public sealed record HttpPointMapping(
    string Path,
    string Method,
    string? JsonPointer,
    string? ValuePointer,
    string ContentType = "application/json") : PointMapping;