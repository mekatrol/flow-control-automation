using Server.Common.Contracts.Templating;

namespace Server.Common.Errors;

/// <summary>Represents an expected template validation or rendering failure.</summary>
public sealed class TemplateRenderException(
    TemplateError category,
    string message,
    IReadOnlyList<TemplateDiagnostic>? diagnostics = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>Gets the stable application-level failure category.</summary>
    public TemplateError Category { get; } = category;

    /// <summary>Gets detailed template diagnostics, when available.</summary>
    public IReadOnlyList<TemplateDiagnostic> Diagnostics { get; } = diagnostics ?? [];
}