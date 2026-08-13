namespace Server.Services.Contracts;

public sealed record FlowDebugInspection
{
    public required ushort InstructionPointer { get; init; }
    public required bool IsAtCommit { get; init; }
    public string? NodeId { get; init; }
    public IReadOnlyList<DebugTypedValue> Slots { get; init; } = [];
    public IReadOnlyList<DebugTypedValue> CurrentState { get; init; } = [];
    public IReadOnlyList<DebugTypedValue?> StagedNextState { get; init; } = [];
    public IReadOnlyList<FlowVmCommand> ProposedOutputs { get; init; } = [];
}
