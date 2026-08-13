namespace Server.Services.Contracts;

public sealed record FlowDebugCapabilities
{
    public bool StepTick { get; init; }
    public bool StepNode { get; init; }
    public bool StepInstruction { get; init; }
    public bool Continue { get; init; }
    public bool Pause { get; init; }
    public bool RunTo { get; init; }
    public int MaximumBreakpoints { get; init; }
    public int MaximumInspectableSlots { get; init; }
}
