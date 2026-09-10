namespace Server.Common.Models.FlowExecution;

/// <summary>Requests a set of input changes for a simulator-capable context.</summary>
public sealed record ApplyFlowExecutionInputs(IReadOnlyList<EmulatorInputChange> Inputs);