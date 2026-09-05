namespace Server.Common.Types;

/// <summary>
/// Describes the lifecycle state of an execution-context deployment.
/// </summary>
public enum ExecutionContextDeploymentStatusType
{
    /// <summary>
    /// The deployment has been defined but is not active.
    /// </summary>
    Draft,

    /// <summary>
    /// The deployment is active on its execution instance.
    /// </summary>
    Active,

    /// <summary>
    /// The deployment is disabled and is not scheduled for execution.
    /// </summary>
    Disabled,

    /// <summary>
    /// The deployment is in a failed state.
    /// </summary>
    Failed
}