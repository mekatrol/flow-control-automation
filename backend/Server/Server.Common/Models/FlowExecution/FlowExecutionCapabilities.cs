namespace Server.Common.Models.FlowExecution;

/// <summary>Advertises the operations supported by a flow execution context.</summary>
public sealed record FlowExecutionCapabilities
{
    private static FlowExecutionCapabilities Core { get; } = new()
    {
        CanRun = true,
        CanPause = true,
        CanStop = true,
        CanRestart = true,
        CanStepTick = true,
        CanStepNode = true,
        CanStepInstruction = true,
        CanUseBreakpoints = true,
        CanRunTo = true
    };

    public bool CanRun { get; init; }
    public bool CanPause { get; init; }
    public bool CanStop { get; init; }
    public bool CanRestart { get; init; }
    public bool CanStepTick { get; init; }
    public bool CanStepNode { get; init; }
    public bool CanStepInstruction { get; init; }
    public bool CanUseBreakpoints { get; init; }
    public bool CanRunTo { get; init; }
    public bool CanEditInputs { get; init; }
    public bool CanAdvanceVirtualTime { get; init; }
    public bool CanInjectFaults { get; init; }
    public bool CanResetIo { get; init; }
    public bool CanEnableLiveOutputs { get; init; }
    public bool LocksFlowEditing { get; init; }

    /// <summary>Creates the capability set for deterministic simulator contexts.</summary>
    public static FlowExecutionCapabilities Simulator { get; } = Core with
    {
        CanEditInputs = true,
        CanAdvanceVirtualTime = true,
        CanInjectFaults = true,
        CanResetIo = true
    };

    /// <summary>Creates the capability set for debugger contexts hosted by the server VM.</summary>
    public static FlowExecutionCapabilities ServerDebugger { get; } = Core with
    {
        LocksFlowEditing = true
    };

    /// <summary>Creates the capability set for controller debugger contexts.</summary>
    /// <param name="canEnableLiveOutputs">Whether the target permits commissioned physical output writes.</param>
    /// <returns>Core debugger capabilities with the target's live-output capability applied.</returns>
    public static FlowExecutionCapabilities ControllerDebugger(bool canEnableLiveOutputs) => Core with
    {
        CanEnableLiveOutputs = canEnableLiveOutputs,
        LocksFlowEditing = true
    };

}