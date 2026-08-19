using Server.Common.Contracts;

namespace Server.Services;

/// <summary>Validates controller-template syntax and compiles its capability sets for efficient compatibility checks.</summary>
public interface IControllerTemplateValidator
{
    /// <summary>Validates one complete template without persisting it.</summary>
    /// <param name="template">The current-schema controller template whose identifiers, capabilities, limits, and connector contracts will be checked.</param>
    /// <param name="allowBuiltInDefault">Whether the reserved built-in template identity is accepted; this is valid only for trusted bootstrap data.</param>
    /// <returns>The original template paired with normalized, duplicate-free capability sets.</returns>
    /// <exception cref="ArgumentException">Thrown when the template violates schema, range, uniqueness, or compatibility constraints.</exception>
    ValidatedControllerTemplate Validate(
        ControllerTemplate template,
        bool allowBuiltInDefault = false);
}