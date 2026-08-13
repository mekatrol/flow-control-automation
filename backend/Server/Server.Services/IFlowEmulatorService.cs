using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowEmulatorService
{
    Task<FlowEmulatorSnapshot> CreateAsync(ExecutableFlowSource source, CancellationToken cancellationToken);
    FlowEmulatorSnapshot Get(string emulatorId);
    FlowEmulatorSnapshot SetInputs(string emulatorId, IReadOnlyList<EmulatorInputChange> changes);
    FlowEmulatorSnapshot Advance(string emulatorId, ulong milliseconds, bool scan);
    FlowEmulatorSnapshot InjectFault(string emulatorId, string? fault);
    FlowEmulatorSnapshot Reset(string emulatorId, bool powerCycle);
    FlowEmulatorScenario ExportScenario(string emulatorId);
    void Delete(string emulatorId);
}
