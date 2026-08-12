namespace Server.Services.Contracts;

/// <summary>
/// Complete, resolved input to executable-flow compilation.
/// </summary>
public sealed record FlowCompilationRequest
{
    /// <summary>
    /// Gets the requested executable artifact version. Production compilation defaults to scheduled Flow IL v2.
    /// </summary>
    public int ArtifactVersion { get; init; } = 2;

    public required ExecutableFlowSource Source { get; init; }
    public required FlowCompilationTarget Target { get; init; }
}