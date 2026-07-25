namespace Server.Services;

public sealed class FlowValidationException(string message)
    : Exception(message);