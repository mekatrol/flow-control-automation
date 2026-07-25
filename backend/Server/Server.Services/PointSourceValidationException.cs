namespace Server.Services;

public sealed class PointSourceValidationException(string message)
    : Exception(message);