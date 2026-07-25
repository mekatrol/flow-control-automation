using Microsoft.Extensions.Configuration;

namespace Server.Services;

public sealed class ServerOptions
{
    public const string AddressConfigurationKey = "SERVER_ADDRESS";

    public const string CredentialEncryptionKeyConfigurationKey = "CREDENTIAL_ENCRYPTION_KEY";

    public const string DefaultAddress = "http://localhost:8080";

    [ConfigurationKeyName(AddressConfigurationKey)]
    public string ServerAddress { get; set; } = DefaultAddress;

    public string? CredentialEncryptionKey { get; set; }

    public static bool HasValidCredentialEncryptionKey(ServerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CredentialEncryptionKey))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(options.CredentialEncryptionKey).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}