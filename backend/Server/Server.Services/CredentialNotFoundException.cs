namespace Server.Services;

public sealed class CredentialNotFoundException(string id)
    : Exception($"Credential {id} was not found.");