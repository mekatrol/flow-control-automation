using Server.Common.Contracts;
using Server.Common.Types;

namespace Server.Common.Models;

public abstract record AutomationPoint : IAutomationPoint
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public bool Enabled { get; set; }

    public string? GroupId { get; set; }

    public required DataDirectionType Direction { get; init; }

    public required AutomationPointValueType ValueType { get; init; }

    public abstract PointSourceType PointSourceType { get; }
}
