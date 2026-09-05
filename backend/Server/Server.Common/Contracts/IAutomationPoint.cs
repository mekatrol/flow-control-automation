using Server.Common.Types;

namespace Server.Common.Contracts;

public interface IAutomationPoint
{
    string Id { get; init; }

    string Name { get; init; }

    string? Description { get; init; }

    bool Enabled { get; set; }

    string? GroupId { get; set; }

    DataDirectionType Direction { get; init; }

    FlowPointValueType ValueType { get; init; }

    PointSourceType PointSourceType { get; init; }
}