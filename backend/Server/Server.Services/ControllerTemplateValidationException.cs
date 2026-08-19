namespace Server.Services;

public sealed class ControllerTemplateValidationException(
    IReadOnlyList<ControllerDiagnostic> diagnostics)
    : Exception(diagnostics.Count == 0
        ? "Controller template is invalid."
        : diagnostics[0].Message)
{
    public IReadOnlyList<ControllerDiagnostic> Diagnostics { get; } = diagnostics;
}