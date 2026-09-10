namespace Server.Services;

public sealed class FlowExecutionContextConflictException(string message) : Exception(message);