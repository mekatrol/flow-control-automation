namespace Server.Services.Contracts;

public sealed record HttpJsonPointMapping(
    string Path,
    string Method,
    string? JsonPointer) : PointMapping;