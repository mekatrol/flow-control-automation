using Server.Common.Types;

namespace Server.Common.Models;

public sealed record ExecutionInstance
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ExecutionInstanceType ExecutionInstanceType { get; init; }
    public string? ControllerTemplateId { get; init; }
    public int? ControllerTemplateRevision { get; init; }
    public string? DeviceIdentity { get; init; }
    public bool Enabled { get; init; } = true;
    public int Revision { get; init; } = 1;
}