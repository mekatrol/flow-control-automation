namespace Server.Services.Contracts;

public sealed record ValidatedPointDefinition(
    Point Source,
    PointImplementation Implementation,
    PointDirection Direction,
    PointValueType ValueType,
    PointPersistence Persistence,
    PointLimits? Limits,
    DigitalStateLabels? DigitalLabels,
    IReadOnlyList<MultiStateLabel>? MultiStateLabels,
    PointSafetyPolicy? SafetyPolicy,
    PointSourceKind? SourceKind,
    PointMapping? Mapping);