using Server.Common.Types;

namespace Server.Compiler.Contracts;

/* Logical decoded form of one fixed 12-byte VM instruction record. */
internal sealed record Instruction(
    FlowOpcodeType Opcode,
    ushort ResultSlotIndex,
    ushort Operand0,
    ushort Operand1,
    ushort Auxiliary);