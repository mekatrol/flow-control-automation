using Server.Common.Models;
using Server.Common.Types;

namespace Server.Services.Contracts;

public sealed record ValidatedPointDefinition(
    FlowPoint Source,
    PointImplementation Implementation,
    DataDirectionType Direction,
    FlowPointValueType ValueType,
    PointPersistence Persistence,
    PointLimits? Limits,
    DigitalStateLabels? DigitalLabels,
    IReadOnlyList<MultiStateLabel>? MultiStateLabels,
    PointSafetyPolicy? SafetyPolicy,
    PointSourceKind? SourceKind,
    PointMapping? Mapping);