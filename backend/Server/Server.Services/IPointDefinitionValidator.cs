using Server.Common.Contracts;

namespace Server.Services;

/// <summary>Enforces point, group, mapping, capability, and cross-reference contracts.</summary>
public interface IPointDefinitionValidator
{
    /// <summary>Validates and normalizes one point against its resolved source and controller capabilities.</summary>
    /// <param name="point">The current-schema point definition to validate; identifiers and vocabulary values must be canonical.</param>
    /// <param name="context">The resolved source and capability context used for mapping and compatibility checks.</param>
    /// <returns>The point paired with parsed domain enums and normalized mapping data.</returns>
    ValidatedPointDefinition Validate(
        FlowPoint point,
        PointValidationContext context);

    /// <summary>Validates one group and every source reference it declares.</summary>
    /// <param name="group">The group to validate; its ID and name must be non-empty and its point/source membership must satisfy group limits.</param>
    /// <param name="sources">All sources addressable by ID; keys must be canonical and every referenced source must exist.</param>
    void ValidateGroup(
        PointGroup group,
        IReadOnlyDictionary<string, PointSource> sources);

    /// <summary>Validates an entire point document as one self-consistent snapshot.</summary>
    /// <param name="document">The current-version document containing unique groups and points with valid revisions.</param>
    /// <param name="sources">All sources available to mappings in the document, keyed by canonical source ID.</param>
    void ValidateDocument(
        PointDocument document,
        IReadOnlyDictionary<string, PointSource> sources);
}