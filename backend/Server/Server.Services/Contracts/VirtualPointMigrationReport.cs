namespace Server.Services.Contracts;

public sealed record VirtualPointMigrationReport(
    bool Applied,
    int FlowsInspected,
    int FlowsChanged,
    int DeclarationsAdded,
    IReadOnlyList<VirtualPointMigrationDiagnostic> Diagnostics);
