namespace Server.Services;

public sealed class FlowSimulatorException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
