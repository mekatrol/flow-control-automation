namespace Server.Services;

public sealed class PointDefinitionValidationException(string message)
    : Exception(message);