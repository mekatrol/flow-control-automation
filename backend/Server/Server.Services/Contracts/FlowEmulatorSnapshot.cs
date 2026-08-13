namespace Server.Services.Contracts;

public sealed record FlowEmulatorSnapshot
{
    public required string EmulatorId { get; init; }
    public required string FlowId { get; init; }
    public required string ControllerTemplateId { get; init; }
    public required string LifecycleState { get; init; }
    public required ulong VirtualTimeMilliseconds { get; init; }
    public required ulong ScanNumber { get; init; }
    public IReadOnlyList<FlowVmInput> Inputs { get; init; } = [];
    public IReadOnlyList<EmulatorOutputSample> OutputHistory { get; init; } = [];
    public string? ActiveFault { get; init; }
}
