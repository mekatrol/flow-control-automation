namespace Server.Services.Contracts;

public sealed record FlowInterfaceOutput
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public string? Units { get; init; }
}
