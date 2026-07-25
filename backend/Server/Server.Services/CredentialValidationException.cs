namespace Server.Services;

public sealed class CredentialValidationException(string message)
    : Exception(message);