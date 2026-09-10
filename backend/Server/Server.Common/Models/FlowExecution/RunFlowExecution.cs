namespace Server.Common.Models.FlowExecution;

/// <summary>Requests continuous execution with a bounded interval between scan cycles.</summary>
public sealed record RunFlowExecution(uint IntervalMilliseconds);