namespace Server.Services.Contracts;

#pragma warning disable SA1402 // Cohesive controller capability enums are kept together.
#pragma warning disable SA1649 // The file contains the complete controller capability vocabulary.

public enum ControllerPointFeature
{
    Read,
    Command,
    Retain,
    Override,
    Relinquish,
    Quality,
    Alarms,
    Trends,
}

public enum ConnectorDataType
{
    Any,
    Boolean,
    Event,
    Number,
    String,
}

public enum ExecutionMode
{
    Event,
    Interval,
}

public enum ControllerRuntimeFeature
{
    VirtualPoints,
    BoundPoints,
    CommandArbitration,
    QualityPropagation,
}

#pragma warning restore SA1649
#pragma warning restore SA1402