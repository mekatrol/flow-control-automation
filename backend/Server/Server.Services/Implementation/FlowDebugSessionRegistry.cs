using Server.Services.Contracts;

namespace Server.Services.Implementation;

public sealed class FlowDebugSessionRegistry : IDisposable
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public FlowDebugSession? Session { get; set; }
    internal LocalFlowDebugSession? Local { get; set; }

    public void Dispose()
    {
        Local?.Dispose();
        Local = null;
        Session = null;
        Gate.Dispose();
    }
}