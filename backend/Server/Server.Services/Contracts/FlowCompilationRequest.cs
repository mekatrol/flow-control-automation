namespace Server.Services.Contracts;

/// <summary>
/// Complete, resolved input to executable-flow compilation.
/// </summary>
public sealed record FlowCompilationRequest
{
    public required ExecutableFlowSource Source { get; init; }
    public required FlowCompilationTarget Target { get; init; }
}
