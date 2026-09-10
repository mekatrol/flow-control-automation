namespace Server.Common.Models.FlowExecution;

/// <summary>Requests deterministic advancement of a simulator-capable context clock.</summary>
public sealed record AdvanceFlowExecution(ulong Milliseconds);