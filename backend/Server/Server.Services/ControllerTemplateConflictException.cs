namespace Server.Services;

public sealed class ControllerTemplateConflictException(string message, Exception? inner = null)
    : Exception(message, inner);