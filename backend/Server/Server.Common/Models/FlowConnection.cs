namespace Server.Common.Models;

public sealed record FlowConnection(string Id, FlowEndpoint Start, FlowEndpoint End);