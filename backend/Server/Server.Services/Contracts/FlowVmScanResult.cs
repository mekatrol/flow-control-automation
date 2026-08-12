namespace Server.Services.Contracts;

public sealed record FlowVmScanResult(
    ulong ScanNumber,
    ulong SampledAtMilliseconds,
    IReadOnlyList<bool> Slots,
    IReadOnlyList<FlowVmCommand> Commands);
