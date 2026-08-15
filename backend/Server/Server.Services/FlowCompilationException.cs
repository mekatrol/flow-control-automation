using Server.Services.Contracts;

namespace Server.Services;

public sealed class FlowCompilationException : Exception
{
    public FlowCompilationException(IReadOnlyList<FlowCompilationDiagnostic> diagnostics)
        : base(CreateMessage(diagnostics))
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.Count == 0)
        {
            throw new ArgumentException("At least one diagnostic is required.", nameof(diagnostics));
        }

        Diagnostics = diagnostics;
    }

    public IReadOnlyList<FlowCompilationDiagnostic> Diagnostics { get; }

    private static string CreateMessage(IReadOnlyList<FlowCompilationDiagnostic>? diagnostics) =>
        diagnostics is { Count: > 0 }
            ? $"Flow compilation failed: {diagnostics[0].Code} at {diagnostics[0].Path}"
            : "Flow compilation failed.";
}