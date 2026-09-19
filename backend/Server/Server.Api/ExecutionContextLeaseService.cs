namespace Server.Api;

internal sealed class ExecutionContextLeaseService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IFlowExecutionContextService>()
                .CleanupExpiredAsync();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IFlowExecutionContextService>()
            .StopAllAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}