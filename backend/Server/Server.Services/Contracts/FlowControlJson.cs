using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Services.Contracts;

public static class FlowControlJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = null;
        options.PropertyNameCaseInsensitive = false;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.ReadCommentHandling = JsonCommentHandling.Disallow;
        options.AllowTrailingCommas = false;
        options.MaxDepth = 64;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        Configure(options);
        return options;
    }
}