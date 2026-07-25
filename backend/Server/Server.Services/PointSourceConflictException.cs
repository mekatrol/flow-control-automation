namespace Server.Services;

public sealed class PointSourceConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);