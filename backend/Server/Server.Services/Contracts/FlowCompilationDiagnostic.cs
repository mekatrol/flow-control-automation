namespace Server.Services.Contracts;

/// <summary>
/// A stable, machine-readable compiler diagnostic. Path uses JSON Pointer syntax.
/// </summary>
public sealed record FlowCompilationDiagnostic(string Code, string Path, string Message);
