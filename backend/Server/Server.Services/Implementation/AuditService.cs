using Server.Data.Context;
using Server.Data.Entities;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class AuditService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IAuditService
{
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
    {
        await WriteGate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            return [.. (await context.AuditRecords.AsNoTracking().ToListAsync(cancellationToken))
                .OrderByDescending(item => item.Created).Select(item => item.Json)];
        }
        finally
        {
            WriteGate.Release();
        }
    }

    public async Task RecordAsync(string actor, string method, string path, int statusCode, CancellationToken cancellationToken)
    {
        await WriteGate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            var id = Guid.NewGuid().ToString("N");
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            context.AuditRecords.Add(new AuditRecordEntity
            {
                Id = id,
                Key = id,
                Created = now,
                Updated = now,
                Json = JsonSerializer.Serialize(new { id, actor, method, path, statusCode, timestamp = now }, FlowControlJson.Options)
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            WriteGate.Release();
        }
    }
}