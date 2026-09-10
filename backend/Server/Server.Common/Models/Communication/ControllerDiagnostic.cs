namespace Server.Common.Models.Communication;

public sealed record ControllerDiagnostic(
    string Code,
    string Path,
    string Message,
    int? Line = null,
    int? Column = null);