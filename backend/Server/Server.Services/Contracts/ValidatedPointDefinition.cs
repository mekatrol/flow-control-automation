using Server.Common.Models;
using Server.Common.Types;

namespace Server.Services.Contracts;

public sealed record ValidatedPointDefinition(
    VirtualAutomationPoint Source,
    PointImplementation Implementation,
    DataDirectionType Direction,
    AutomationPointValueType ValueType,
    PointPersistence Persistence,
    PointLimits? Limits,
    DigitalStateLabels? DigitalLabels,
    IReadOnlyList<MultiStateLabel>? MultiStateLabels,
    PointSafetyPolicy? SafetyPolicy,
    PointSourceKind? SourceKind,
    PointMapping? Mapping);