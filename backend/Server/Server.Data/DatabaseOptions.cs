using Microsoft.Extensions.Configuration;

namespace Server.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public const string FlowControlConfigurationKey = "FlowControl";

    public const string DefaultConnectionString = "Data Source=flow-control.db";

    [ConfigurationKeyName(FlowControlConfigurationKey)]
    public string ConnectionString { get; set; } = DefaultConnectionString;
}