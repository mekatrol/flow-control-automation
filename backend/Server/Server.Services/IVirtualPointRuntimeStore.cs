using Server.Common.Models;

namespace Server.Services;

public interface IVirtualPointRuntimeStore
{
    Task ActivateFlowAsync(string executionInstanceId, string flowId, IReadOnlyList<VirtualPointDeclaration> declarations, IReadOnlySet<string> writerKeys, CancellationToken cancellationToken);
    void ReleaseFlow(string executionInstanceId, string flowId);
    bool TrySnapshot(string executionInstanceId, string pointKey, out VirtualPointRuntimeValue value);
    Task CommitAsync(string executionInstanceId, string flowId, IReadOnlyList<FlowVmCommand> commands, CancellationToken cancellationToken);
    IReadOnlyList<VirtualPointRuntimeValue> List(string executionInstanceId);
    Task ClearRetainedAsync(string executionInstanceId, CancellationToken cancellationToken);
    Task RestoreRetainedAsync(string executionInstanceId, IReadOnlyDictionary<string, RetainedVirtualPointValue> values, CancellationToken cancellationToken);
}