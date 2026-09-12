namespace Server.Common.Models.Points;

public sealed record MappingReadOperation
{
    public string? Path { get; init; }
    public string? Method { get; init; }
    public string? Format { get; init; }
    public string? Template { get; init; }
    public string? Topic { get; init; }
    public int? Qos { get; init; }
    public string? EntityId { get; init; }
    public string? Property { get; init; }
    public int? PollMilliseconds { get; init; }
}