namespace Server.Services.Implementation;

public sealed class ManagedFlowVirtualMachineFactory : IFlowVirtualMachineFactory
{
    public IFlowVirtualMachine Create(ReadOnlyMemory<byte> artifact) =>
        new ManagedFlowVirtualMachine(artifact);
}