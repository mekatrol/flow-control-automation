using Server.Common.Contracts;

namespace Server.Compiler.Contracts;

/* Logical decoded form of one fixed 12-byte VM instruction record. */
internal sealed record Instruction(
    FlowOpcode Opcode,
    ushort ResultSlotIndex,
    ushort Operand0,
    ushort Operand1,
    ushort Auxiliary);