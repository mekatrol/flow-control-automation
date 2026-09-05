using Server.Common.Types;

namespace Server.Common.Models;

public sealed record PhysicalAutomationPoint : AutomationPoint
{
    public override PointSourceType PointSourceType => PointSourceType.Physical;
}