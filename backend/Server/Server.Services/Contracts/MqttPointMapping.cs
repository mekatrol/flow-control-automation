namespace Server.Services.Contracts;

public sealed record MqttPointMapping(
    string? StateTopic,
    string? CommandTopic,
    int Qos,
    bool Retain,
    string? JsonPointer) : PointMapping;
