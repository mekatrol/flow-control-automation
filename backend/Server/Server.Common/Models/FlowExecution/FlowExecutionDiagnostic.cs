namespace Server.Common.Models.FlowExecution;

/// <summary>Describes a stable execution failure and its optional source location.</summary>
public sealed record FlowExecutionDiagnostic
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Path { get; init; }
}