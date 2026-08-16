namespace Server.Services.Contracts;

public enum DataQuality : byte
{
    Good,

    Bad,

    Stale,

    Unavailable
}