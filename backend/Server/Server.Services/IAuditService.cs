namespace Server.Services;

public interface IAuditService
{
    Task RecordAsync(string actor, string method, string path, int statusCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);
}