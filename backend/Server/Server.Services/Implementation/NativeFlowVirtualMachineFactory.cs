namespace Server.Services.Implementation;

internal sealed class NativeFlowVirtualMachineFactory : IFlowVirtualMachineFactory
{
    public IFlowVirtualMachine Create(ReadOnlyMemory<byte> artifact) =>
        new NativeFlowVirtualMachine(artifact);
}