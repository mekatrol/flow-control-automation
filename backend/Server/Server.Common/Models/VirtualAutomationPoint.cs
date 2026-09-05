using Server.Common.Types;

namespace Server.Common.Models;

public sealed record VirtualAutomationPoint : AutomationPoint
{
    public override PointSourceType PointSourceType => PointSourceType.Virtual;
}
