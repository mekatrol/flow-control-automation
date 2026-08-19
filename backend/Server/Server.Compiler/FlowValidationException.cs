namespace Server.Compiler;

public sealed class FlowValidationException(string message)
    : Exception(message);