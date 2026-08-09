using Server.Services.Contracts;

namespace Server.Services.Implementation;

public sealed class FlowDebugSessionRegistry
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public FlowDebugSession? Session { get; set; }
}
