namespace Server.Services.Contracts;

public sealed record FlowVmExecutionFrame(
    ushort InstructionIndex,
    byte Opcode,
    bool IsAtCommit,
    IReadOnlyList<FlowVmValue> Slots,
    IReadOnlyList<bool> CurrentState,
    IReadOnlyList<bool?> StagedState,
    IReadOnlyList<FlowVmCommand> ProposedCommands);
