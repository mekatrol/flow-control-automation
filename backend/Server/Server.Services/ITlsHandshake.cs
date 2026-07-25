namespace Server.Services;

public interface ITlsHandshake
{
    Task<Stream> AuthenticateAsync(
        Stream stream,
        string host,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}