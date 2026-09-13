namespace Server.Common.Contracts.Points;

/// <summary>Enforces point, mapping, capability, and cross-reference contracts.</summary>
public interface IPointDefinitionValidator
{
    /// <summary>Validates and normalizes one point against its resolved source and controller capabilities.</summary>
    /// <param name="point">The current-schema point definition to validate; identifiers and vocabulary values must be canonical.</param>
    /// <param name="context">The resolved source and capability context used for mapping and compatibility checks.</param>
    /// <returns>The point paired with parsed domain enums and normalized mapping data.</returns>
    ValidatedPointDefinition Validate(
        AutomationPoint point,
        PointValidationContext context);

    /// <summary>Validates all aggregate-owned points as one self-consistent projection.</summary>
    /// <param name="points">The points projected from all current source aggregates.</param>
    /// <param name="sources">All sources available to mappings in the document, keyed by canonical source ID.</param>
    void ValidateDocument(
        IReadOnlyList<AutomationPoint> points,
        IReadOnlyDictionary<string, PointSource> sources);
}