namespace Server.Services.Contracts;

public sealed class ConfigurationYamlException : Exception
{
    public ConfigurationYamlException(ConfigurationYamlError category, string message)
        : base(message)
    {
        Category = category;
    }

    public ConfigurationYamlException(
        ConfigurationYamlError category,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Category = category;
    }

    public ConfigurationYamlError Category { get; }
}