namespace Server.Services.Contracts;

public sealed record VirtualPointMigrationDiagnostic(
    string FlowId,
    string Code,
    string Severity,
    string Message,
    string? NodeId = null,
    string? PointId = null);