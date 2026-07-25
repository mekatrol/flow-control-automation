namespace Server.Services;

public sealed class PointDefinitionConflictException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);