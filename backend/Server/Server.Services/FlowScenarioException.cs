namespace Server.Services;

public sealed class FlowScenarioException(string code, string message, string? path = null) : Exception(message)
{
    public string Code { get; } = code;
    public string? Path { get; } = path;
}