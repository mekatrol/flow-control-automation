namespace Server.Services.Contracts;

#pragma warning disable SA1402 // Cohesive point-domain value types are kept together.
#pragma warning disable SA1649 // The file contains the complete point-domain vocabulary.

public enum PointImplementation
{
    Virtual,
    Bound,
}

public enum PointDirection
{
    Input,
    Output,
    InputOutput,
    Value,
}

public enum PointValueType
{
    Analog,
    Digital,
    MultiState,
    Integer,
    Text,
}

public enum PointPersistence
{
    Volatile,
    Retained,
}

public enum PointSourceKind
{
    HomeAssistant,
    Mqtt,
    HttpJson,
}

public sealed record DigitalStateLabels(string False, string True);

public sealed record MultiStateLabel(string Key, string Label);

public sealed record PointLimits(
    double? Minimum,
    double? Maximum,
    int? MaximumLength);

public sealed record PointSafetyPolicy(
    string Startup,
    string Shutdown,
    string CommunicationLoss,
    string Disable);

public abstract record PointMapping;

public sealed record HomeAssistantPointMapping(
    string EntityId,
    string? StateProperty,
    string? CommandService) : PointMapping;

public sealed record MqttPointMapping(
    string? StateTopic,
    string? CommandTopic,
    int Qos,
    bool Retain,
    string? JsonPointer) : PointMapping;

public sealed record HttpJsonPointMapping(
    string Path,
    string Method,
    string? JsonPointer) : PointMapping;

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

public sealed record PointValidationContext(
    IReadOnlyDictionary<string, PointGroup> Groups,
    IReadOnlyDictionary<string, PointSource> Sources)
{
    public static PointValidationContext Empty { get; } = new(
        new Dictionary<string, PointGroup>(StringComparer.Ordinal),
        new Dictionary<string, PointSource>(StringComparer.Ordinal));
}

#pragma warning restore SA1649
#pragma warning restore SA1402