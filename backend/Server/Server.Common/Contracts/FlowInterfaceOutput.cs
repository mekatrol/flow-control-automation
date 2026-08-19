namespace Server.Common.Contracts;

public sealed record FlowInterfaceOutput
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required DataType DataType { get; init; }

    public string? Units { get; init; }
}