namespace Server.Common.Contracts.Templating;

/// <summary>Contains the result of validating template syntax.</summary>
/// <param name="Diagnostics">The syntax diagnostics; empty when the template is valid.</param>
public sealed record TemplateValidationResult(IReadOnlyList<TemplateDiagnostic> Diagnostics)
{
    /// <summary>Gets a value indicating whether the template is valid.</summary>
    public bool IsValid => Diagnostics.Count == 0;
}