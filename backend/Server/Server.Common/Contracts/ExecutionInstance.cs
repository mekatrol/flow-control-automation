namespace Server.Common.Contracts;

public sealed record ExecutionInstance
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ExecutionInstanceKind Kind { get; init; }
    public string? ControllerTemplateId { get; init; }
    public int? ControllerTemplateRevision { get; init; }
    public string? DeviceIdentity { get; init; }
    public bool Enabled { get; init; } = true;
    public int Revision { get; init; } = 1;
}
