namespace Server.Services;

public sealed class ControllerGatewayException(string category, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Category { get; } = category;
}
