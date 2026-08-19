using Server.Common.Contracts;

namespace Server.Services.Implementation;

internal sealed class LocalFlowDebugSession(
    IFlowVirtualMachine machine,
    ExecutableFlowSource source,
    FlowCompilationResult compilation,
    string host,
    string sessionId,
    FlowEmulatorService.Instance? emulator) : IDisposable
{
    public IFlowVirtualMachine Machine { get; } = machine;
    public ExecutableFlowSource Source { get; } = source;
    public FlowCompilationResult Compilation { get; } = compilation;
    public string Host { get; } = host;
    public string SessionId { get; } = sessionId;
    public FlowEmulatorService.Instance? Emulator { get; } = emulator;
    public FlowVmExecutionFrame? Frame { get; set; }
    public IReadOnlyList<FlowDebugBreakpoint> Breakpoints { get; set; } = [];

    public void Dispose()
    {
        if (Frame is not null)
        {
            Machine.AbortScan();
        }

        Machine.Dispose();
    }
}