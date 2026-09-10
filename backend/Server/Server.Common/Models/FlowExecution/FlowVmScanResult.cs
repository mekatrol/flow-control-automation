namespace Server.Common.Models.FlowExecution;

public sealed record FlowVmScanResult(
    ulong ScanNumber,
    ulong SampledAtMilliseconds,
    IReadOnlyList<FlowVmValue> Slots,
    IReadOnlyList<FlowVmCommand> Commands);