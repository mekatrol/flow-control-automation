namespace Server.Common.Models.FlowExecution;

/// <summary>Requests resetting simulator I/O, optionally as a power cycle.</summary>
public sealed record ResetFlowExecutionIo(bool PowerCycle);