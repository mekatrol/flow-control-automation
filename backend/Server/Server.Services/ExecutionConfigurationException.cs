namespace Server.Services;

public sealed class ExecutionConfigurationException(
    string message,
    int statusCode = 400,
    string? code = null,
    object? details = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code ?? DefaultCode(statusCode);
    public object? Details { get; } = details;

    private static string DefaultCode(int statusCode) => statusCode switch
    {
        404 => "not_found",
        409 => "resource_conflict",
        422 => "validation_failed",
        _ => "invalid_request"
    };
}