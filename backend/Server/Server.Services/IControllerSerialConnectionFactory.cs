namespace Server.Services;

public interface IControllerSerialConnectionFactory
{
    Task<Stream> ConnectAsync(CancellationToken cancellationToken);
}
