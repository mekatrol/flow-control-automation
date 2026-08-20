namespace Server.Compiler.Contracts;

/// <summary>
/// A stable, machine-readable compiler diagnostic. Path uses JSON Pointer syntax.
/// </summary>
public sealed record FlowCompilationDiagnostic(
    FlowCompilationDiagnosticCode Code,
    string DisplayCode,
    string Path,
    string Title,
    string Message)
{
    public override string ToString() =>
        string.IsNullOrEmpty(Path)
            ? $"{DisplayCode}: {Message}"
            : $"{DisplayCode}: {Message} ({Path})";
}