namespace Server.Common.Contracts;

public sealed record FlowConnector(
    string Id,
    string Label,
    DataDirection Direction,
    DataType DataType,
    string Side);