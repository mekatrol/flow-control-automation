namespace Server.Services.Contracts;

public sealed record PointSourceConnection
{
    public string? BaseUrl { get; init; }
    public bool? SubscribeEvents { get; init; }
    public string? BrokerUrl { get; init; }
    public string? ClientIdPrefix { get; init; }
    public string? TestTopic { get; init; }
    public int? Qos { get; init; }
    public bool? CleanStart { get; init; }
    public int? KeepAliveSeconds { get; init; }
    public IReadOnlyList<string>? AllowedReadMethods { get; init; }
    public int? DefaultPollMilliseconds { get; init; }
    public bool? FollowRedirects { get; init; }
    public long? MaximumResponseBytes { get; init; }
    public bool? AllowPrivateNetwork { get; init; }
}