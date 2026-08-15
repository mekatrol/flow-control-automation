namespace Server.Services.Contracts;

public sealed record RuntimeSnapshot(
    string FlowId,
    string State,
    string UpdatedAt,
    IReadOnlyDictionary<string, NodeRuntimeSnapshot> Nodes)
{
    public ulong ScanNumber { get; init; }
    public string Diagnostic { get; init; } = string.Empty;
    public double ReadInputsMilliseconds { get; init; }
    public double ExecuteLogicMilliseconds { get; init; }
    public double WriteOutputsMilliseconds { get; init; }
    public IReadOnlyDictionary<string, bool> Outputs { get; init; }
        = new Dictionary<string, bool>(StringComparer.Ordinal);
}