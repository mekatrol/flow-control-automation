namespace Server.Services.FlowExecution.VirtualMachine;

internal sealed class ManagedFlowVirtualMachineFactory : IFlowVirtualMachineFactory
{
    public IFlowVirtualMachine Create(ReadOnlyMemory<byte> artifact) =>
        new ManagedFlowVirtualMachine(artifact);
}