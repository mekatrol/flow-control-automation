namespace Server.Common.Models.FlowExecution;

/// <summary>Contains optional input, output, clock, fault, and live-output context state.</summary>
public sealed record FlowExecutionIo
{
    public IReadOnlyList<FlowVmInput> Inputs { get; init; } = [];
    public IReadOnlyList<EmulatorOutputSample> OutputHistory { get; init; } = [];
    public ulong? VirtualTimeMilliseconds { get; init; }
    public string? ActiveFault { get; init; }
    public bool LiveOutputEnabled { get; init; }
    public IReadOnlyList<string> LiveOutputPointIds { get; init; } = [];
}