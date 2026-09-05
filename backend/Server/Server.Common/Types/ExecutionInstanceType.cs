namespace Server.Common.Types;

/// <summary>
/// Identifies the host on which an execution context runs.
/// </summary>
public enum ExecutionInstanceType
{
    /// <summary>
    /// The execution context runs on the server runtime.
    /// </summary>
    Server,

    /// <summary>
    /// The execution context runs on a controller device.
    /// </summary>
    Controller
}