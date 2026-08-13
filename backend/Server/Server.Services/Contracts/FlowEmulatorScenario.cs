namespace Server.Services.Contracts;

public sealed record FlowEmulatorScenario(
    IReadOnlyList<EmulatorInputChange> Inputs,
    IReadOnlyList<EmulatorOutputSample> ExpectedOutputs);
