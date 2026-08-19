namespace Server.Services.Contracts;

/// <summary>
/// A stable, machine-readable compiler diagnostic. Path uses JSON Pointer syntax.
/// </summary>
public sealed record FlowCompilationDiagnostic(FlowCompilerCode Code, string Path, string Message);