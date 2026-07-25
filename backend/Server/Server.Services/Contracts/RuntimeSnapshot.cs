namespace Server.Services.Contracts;

public sealed record RuntimeSnapshot(
    string FlowId,
    string State,
    string UpdatedAt,
    IReadOnlyDictionary<string, NodeRuntimeSnapshot> Nodes);