namespace Server.Services.Extensions;

public static class DataTypeExtensions
{
    public const string Any = "any";
    public const string Boolean = "boolean";
    public const string Number = "number";
    public const string String = "string";
    public const string Event = "event";

    public static string ToFriendlyString(this DataType dataType) => dataType switch
    {
        DataType.Any => Any,
        DataType.Boolean => Boolean,
        DataType.Number => Number,
        DataType.String => String,
        DataType.Event => Event,
        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
    };

    public static DataType FromFriendlyString(string friendlyString) => friendlyString switch
    {
        Any => DataType.Any,
        Boolean => DataType.Boolean,
        Number => DataType.Number,
        String => DataType.String,
        Event => DataType.Event,
        _ => throw new ArgumentOutOfRangeException(nameof(friendlyString), friendlyString, null)
    };
}
