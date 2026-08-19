using Server.Common.Contracts;

namespace Server.Services.Contracts;

public sealed record ValidatedPointDefinition(
    FlowPoint Source,
    PointImplementation Implementation,
    DataDirection Direction,
    FlowPointValueType ValueType,
    PointPersistence Persistence,
    PointLimits? Limits,
    DigitalStateLabels? DigitalLabels,
    IReadOnlyList<MultiStateLabel>? MultiStateLabels,
    PointSafetyPolicy? SafetyPolicy,
    PointSourceKind? SourceKind,
    PointMapping? Mapping);