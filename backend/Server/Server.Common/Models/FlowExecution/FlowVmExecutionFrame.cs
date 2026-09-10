namespace Server.Common.Models.FlowExecution;

public sealed record FlowVmExecutionFrame(
    ushort InstructionIndex,
    FlowOpcodeType Opcode,
    bool IsAtCommit,
    IReadOnlyList<FlowVmValue> Slots,
    IReadOnlyList<bool> CurrentState,
    IReadOnlyList<bool?> StagedState,
    IReadOnlyList<FlowVmCommand> ProposedCommands);