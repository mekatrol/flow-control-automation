namespace Server.Services.Contracts;

public sealed record FcpClientOptions
{
    public ushort ControllerAddress { get; init; }
    public ushort HostAddress { get; init; } = 0xfffe;
    public required byte[] AuthenticationKey { get; init; }
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(2);
}