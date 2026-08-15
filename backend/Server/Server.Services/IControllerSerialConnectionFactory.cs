namespace Server.Services;

/// <summary>Creates an owned byte stream connected to the configured hardware controller.</summary>
public interface IControllerSerialConnectionFactory
{
    /// <summary>Opens and configures the controller serial connection for FCP traffic.</summary>
    /// <param name="cancellationToken">Cancels connection establishment; cancellation must not leave an open serial port.</param>
    /// <returns>An open, readable, writable stream owned by the caller, which must dispose it after use.</returns>
    Task<Stream> ConnectAsync(CancellationToken cancellationToken);
}