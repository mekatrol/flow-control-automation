namespace Server.Services.Contracts;

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
