namespace Server.Common.Contracts;

/// <summary>
/// Defines how execution of a flow is triggered.
/// </summary>
public enum FlowExecutionMode : byte
{
    /// <summary>
    /// Execution is initiated explicitly by the runtime.
    /// </summary>
    Manual = 1
}