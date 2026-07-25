using Microsoft.Extensions.Configuration;

namespace Server.Services;

public sealed class ServerOptions
{
    public const string AddressConfigurationKey = "SERVER_ADDRESS";

    public const string CredentialEncryptionKeyConfigurationKey = "CREDENTIAL_ENCRYPTION_KEY";

    public const string DefaultAddress = "http://localhost:8080";

    [ConfigurationKeyName(AddressConfigurationKey)]
    public string ServerAddress { get; set; } = DefaultAddress;

    [ConfigurationKeyName(CredentialEncryptionKeyConfigurationKey)]
    public string? CredentialEncryptionKey { get; set; }
}