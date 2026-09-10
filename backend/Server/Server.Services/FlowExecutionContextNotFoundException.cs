namespace Server.Services;

public sealed class FlowExecutionContextNotFoundException(string id)
    : Exception($"Execution context '{id}' was not found.");