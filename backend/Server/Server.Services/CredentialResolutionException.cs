namespace Server.Services;

public sealed class CredentialResolutionException(string message)
    : Exception(message);