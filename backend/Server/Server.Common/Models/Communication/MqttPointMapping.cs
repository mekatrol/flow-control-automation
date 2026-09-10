namespace Server.Common.Models.Communication;

public sealed record MqttPointMapping(
    string? StateTopic,
    string? CommandTopic,
    int Qos,
    bool Retain,
    string? JsonPointer) : PointMapping;