namespace Server.Common.Models.FlowExecution;

public sealed record FlowDebugBreakpoint(string NodeId, string Position = "before", ushort? InstructionDiscriminator = null);