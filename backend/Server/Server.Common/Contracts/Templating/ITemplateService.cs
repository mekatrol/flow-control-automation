namespace Server.Common.Contracts.Templating;

/// <summary>Validates and renders transport-independent text templates.</summary>
public interface ITemplateService
{
    /// <summary>Validates template syntax without rendering it.</summary>
    /// <param name="template">The Scriban template text.</param>
    /// <returns>A result containing any syntax diagnostics.</returns>
    TemplateValidationResult Validate(string template);

    /// <summary>Renders a template using only the explicitly supplied values.</summary>
    /// <param name="template">The Scriban template text.</param>
    /// <param name="values">The case-sensitive values exposed to the template.</param>
    /// <returns>The rendered text.</returns>
    /// <exception cref="TemplateRenderException">The template is invalid or cannot be rendered safely.</exception>
    string Render(
        string template,
        IReadOnlyDictionary<string, object?> values);

    /// <summary>Renders a template using the public members of the supplied model.</summary>
    /// <param name="template">The Scriban template text.</param>
    /// <param name="model">The object model exposed to Scriban.</param>
    /// <returns>The rendered text.</returns>
    /// <exception cref="TemplateRenderException">The template is invalid or cannot be rendered safely.</exception>
    string Render(string template, object model);
}
