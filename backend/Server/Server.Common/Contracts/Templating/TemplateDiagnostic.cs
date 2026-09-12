namespace Server.Common.Contracts.Templating;

/// <summary>Describes a template validation or rendering failure.</summary>
/// <param name="Category">The stable application-level error category.</param>
/// <param name="Message">A human-readable diagnostic message.</param>
/// <param name="Line">The one-based source line, when available.</param>
/// <param name="Column">The one-based source column, when available.</param>
public sealed record TemplateDiagnostic(
    TemplateError Category,
    string Message,
    int? Line = null,
    int? Column = null);