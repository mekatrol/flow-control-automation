namespace Server.Common.Models.Points;

public sealed record RetainedVirtualPointValue(
    FlowVmValue Value,
    string Timestamp,
    ulong Version,
    Server.Common.Models.VirtualPointDefinition Contract);