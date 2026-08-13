namespace Server.Services.Contracts;

public sealed record FlowDebugBreakpoint(string NodeId, string Position = "before", ushort? InstructionDiscriminator = null);
