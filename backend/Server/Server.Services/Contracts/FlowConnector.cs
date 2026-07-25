namespace Server.Services.Contracts;

public sealed record FlowConnector(
    string Id,
    string Label,
    string Direction,
    string DataType,
    string Side);