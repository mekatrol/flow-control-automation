using Server.Common.Types;

namespace Server.Services.Contracts;

public sealed record FlowVmExecutionFrame(
    ushort InstructionIndex,
    FlowOpcodeType Opcode,
    bool IsAtCommit,
    IReadOnlyList<FlowVmValue> Slots,
    IReadOnlyList<bool> CurrentState,
    IReadOnlyList<bool?> StagedState,
    IReadOnlyList<FlowVmCommand> ProposedCommands);