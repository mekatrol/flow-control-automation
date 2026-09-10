namespace Server.Common.Models.FlowExecution;

/// <summary>Requests applying or clearing a synthetic simulator fault.</summary>
public sealed record InjectFlowExecutionFault(string? Fault);