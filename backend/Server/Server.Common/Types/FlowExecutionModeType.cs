namespace Server.Common.Types;

/// <summary>
/// Defines how execution of a flow is triggered.
/// </summary>
public enum FlowExecutionModeType : byte
{
    /// <summary>
    /// Execution is initiated explicitly by the runtime.
    /// </summary>
    Manual = 1
}