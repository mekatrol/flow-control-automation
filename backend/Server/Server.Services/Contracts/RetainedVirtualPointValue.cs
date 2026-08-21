namespace Server.Services.Contracts;

public sealed record RetainedVirtualPointValue(
    FlowVmValue Value,
    string Timestamp,
    ulong Version,
    Server.Common.Contracts.VirtualPointDeclaration Contract);