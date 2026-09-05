namespace Server.Common.Models;

public enum DataQuality : byte
{
    Good = 1,

    Bad,

    Uncertain,

    Unavailable
}