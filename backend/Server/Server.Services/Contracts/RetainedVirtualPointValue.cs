using Server.Common.Contracts;

namespace Server.Services.Contracts;

public sealed record RetainedVirtualPointValue(
    FlowVmValue Value,
    string Timestamp,
    ulong Version);