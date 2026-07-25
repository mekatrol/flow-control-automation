namespace Server.Services.Contracts;

public sealed record FlowConnection(string Id, FlowEndpoint Start, FlowEndpoint End);