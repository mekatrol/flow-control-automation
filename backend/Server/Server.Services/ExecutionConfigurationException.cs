namespace Server.Services;

public sealed class ExecutionConfigurationException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}