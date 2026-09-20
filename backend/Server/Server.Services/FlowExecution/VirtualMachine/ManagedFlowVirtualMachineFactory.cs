namespace Server.Services.FlowExecution.VirtualMachine;

internal sealed class ManagedFlowVirtualMachineFactory(TimeProvider timeProvider) : IFlowVirtualMachineFactory
{
    public IFlowVirtualMachine Create(ReadOnlyMemory<byte> artifact) =>
        new ManagedFlowVirtualMachine(artifact, timeProvider);
}