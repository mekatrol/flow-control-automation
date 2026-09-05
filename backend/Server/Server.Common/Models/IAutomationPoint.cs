using Server.Common.Contracts;

namespace Server.Common.Models;

public interface IAutomationPoint
{
    string Id { get; init; }

    string Name { get; init; }

    string? Description { get; init; }

    bool Enabled { get; set; }

    string? GroupId { get; set; }

    DataDirection Direction { get; init; }

    FlowPointValueType ValueType { get; init; }

    PointSourceType PointSourceType { get; init; }
}