namespace Server.Common.Contracts;

public enum DataQuality : byte
{
    Good = 1,

    Bad,

    Uncertain,

    Unavailable
}