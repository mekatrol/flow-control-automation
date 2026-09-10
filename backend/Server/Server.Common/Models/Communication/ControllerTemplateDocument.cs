namespace Server.Common.Models.Communication;

public sealed record ControllerTemplateDocument
{
    public int SchemaVersion { get; init; } = 1;
    public int Revision { get; init; }
    public IReadOnlyList<ControllerTemplate> Templates { get; init; } = [];
}