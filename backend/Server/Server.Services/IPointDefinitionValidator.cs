using Server.Services.Contracts;

namespace Server.Services;

public interface IPointDefinitionValidator
{
    ValidatedPointDefinition Validate(
        Point point,
        PointValidationContext context);

    void ValidateGroup(
        PointGroup group,
        IReadOnlyDictionary<string, PointSource> sources);

    void ValidateDocument(
        PointDocument document,
        IReadOnlyDictionary<string, PointSource> sources);
}