namespace Server.Common.Models.FlowExecution;

/// <summary>Requests physical live output for explicitly confirmed point IDs.</summary>
public sealed record EnableFlowExecutionLiveOutput(IReadOnlyList<string> PointIds);