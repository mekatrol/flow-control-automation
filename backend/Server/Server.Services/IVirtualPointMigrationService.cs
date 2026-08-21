namespace Server.Services;

public interface IVirtualPointMigrationService
{
    Task<VirtualPointMigrationReport> RunAsync(bool apply, CancellationToken cancellationToken);
}