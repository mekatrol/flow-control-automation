namespace Server.Common.Types;

/// <summary>Identifies the execution host behavior selected for a flow context.</summary>
public enum FlowExecutionMode
{
    /// <summary>Runs the flow against deterministic simulated inputs and virtual time.</summary>
    Simulator,

    /// <summary>Runs the flow through a debugger attached to a server or controller host.</summary>
    Debugger
}