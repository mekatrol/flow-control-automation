using Server.Common.Types;

namespace Server.Common.Models;

public sealed record RemoteAutomationPoint : AutomationPoint
{
    public override PointSourceType PointSourceType => PointSourceType.Remote;
}