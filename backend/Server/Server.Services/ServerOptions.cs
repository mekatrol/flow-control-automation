namespace Server.Services;

public sealed class ServerOptions
{
    public const string AddressConfigurationKey = "SERVER_ADDRESS";

    public const string CredentialEncryptionKeyConfigurationKey = "CREDENTIAL_ENCRYPTION_KEY";
    public const string ControllerDataFileConfigurationKey = "CONTROLLER_DATA_FILE";

    public const string DefaultAddress = "http://localhost:8080";

    [ConfigurationKeyName(AddressConfigurationKey)]
    public string ServerAddress { get; set; } = DefaultAddress;

    public string? CredentialEncryptionKey { get; set; }

    public string ControllerDataFile { get; set; } = Path.Combine("data", "controllers.json");

    public static bool HasValidCredentialEncryptionKey(ServerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CredentialEncryptionKey))
        {
            return false;
        }

        try
        {
            /*  This key can be generated with:
             *
             *  C#
             *       using System;
             *       using System.Security.Cryptography;
             *
             *       byte[] key = RandomNumberGenerator.GetBytes(32);
             *       string base64Key = Convert.ToBase64String(key);
             *
             *       Console.WriteLine(base64Key);
             *  openssl:
             *       openssl rand -base64 32
             *  PowerShell:
             *       $key = New-Object byte[] 32
             *       [System.Security.Cryptography.RandomNumberGenerator]::Fill($key)
             *       [Convert]::ToBase64String($key)
             */

            return Convert.FromBase64String(options.CredentialEncryptionKey).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}