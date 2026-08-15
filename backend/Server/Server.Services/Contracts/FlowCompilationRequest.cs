namespace Server.Services.Contracts;

/// <summary>
/// Complete, resolved input to executable-flow compilation.
/// </summary>
public sealed record FlowCompilationRequest
{
    /// <summary>
    /// Gets the requested executable artifact version. Production compilation uses scheduled Flow IL v1.
    /// </summary>
    public int ArtifactVersion { get; init; } = 1;

    public required ExecutableFlowSource Source { get; init; }
    public required FlowCompilationTarget Target { get; init; }
}