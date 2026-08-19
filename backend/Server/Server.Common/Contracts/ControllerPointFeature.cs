namespace Server.Common.Contracts;

public enum ControllerPointFeature : byte
{
    Read,
    Command,
    Retain,
    Override,
    Relinquish,
    Quality,
    Alarms,
    Trends
}
