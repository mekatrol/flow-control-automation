namespace Server.Services.Extensions;

public static class DataQualityExtensions
{
    public const string Good = "good";
    public const string Bad = "bad";
    public const string Stale = "stale";
    public const string Unavailable = "unavailable";

    public static string ToFriendlyString(this DataQuality dataQuality) => dataQuality switch
    {
        DataQuality.Good => Good,
        DataQuality.Bad => Bad,
        DataQuality.Stale => Stale,
        DataQuality.Unavailable => Unavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(dataQuality), dataQuality, null)
    };

    public static DataQuality FromFriendlyString(string friendlyString) => friendlyString switch
    {
        Good => DataQuality.Good,
        Bad => DataQuality.Bad,
        Stale => DataQuality.Stale,
        Unavailable => DataQuality.Unavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(friendlyString), friendlyString, null)
    };
}