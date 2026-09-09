using Server.Common.Models;
using Server.Compiler.Contracts;

namespace Server.Services.Implementation;

internal sealed class LocalFlowDebugSession(
    IFlowVirtualMachine machine,
    ExecutableFlowSource source,
    FlowCompilationResult compilation,
    string host,
    string sessionId,
    FlowEmulatorService.Instance? emulator) : IDisposable
{
    private CancellationTokenSource? _continuousCancellation;

    public IFlowVirtualMachine Machine { get; } = machine;
    public ExecutableFlowSource Source { get; } = source;
    public FlowCompilationResult Compilation { get; } = compilation;
    public string Host { get; } = host;
    public string SessionId { get; } = sessionId;
    public FlowEmulatorService.Instance? Emulator { get; } = emulator;
    public FlowVmExecutionFrame? Frame { get; set; }
    public IReadOnlyList<FlowDebugBreakpoint> Breakpoints { get; set; } = [];

    public void StartContinuous(Func<CancellationToken, Task> scan, uint intervalMilliseconds)
    {
        StopContinuous();
        _continuousCancellation = new CancellationTokenSource();
        var token = _continuousCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(checked((int)Math.Max(1, intervalMilliseconds)), token);
                    await scan(token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }, CancellationToken.None);
    }

    public void StopContinuous()
    {
        _continuousCancellation?.Cancel();
        _continuousCancellation?.Dispose();
        _continuousCancellation = null;
    }

    public void Dispose()
    {
        StopContinuous();

        if (Frame is not null)
        {
            Machine.AbortScan();
        }

        Machine.Dispose();
    }
}
