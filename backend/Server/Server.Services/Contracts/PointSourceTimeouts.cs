namespace Server.Services.Contracts;

public sealed record PointSourceTimeouts
{
    public int ConnectMilliseconds { get; init; }
    public int? RequestMilliseconds { get; init; }
}