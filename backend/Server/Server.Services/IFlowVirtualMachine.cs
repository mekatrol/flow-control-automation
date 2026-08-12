using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowVirtualMachine : IDisposable
{
    FlowVmScanResult Scan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds);

    void Reset();
}

public interface IFlowVirtualMachineFactory
{
    IFlowVirtualMachine Create(ReadOnlyMemory<byte> artifact);
}
