namespace Server.Services;

public sealed class CredentialConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);