namespace Server.Common.Types;

/// <summary>Describes the current lifecycle state of a flow execution context.</summary>
public enum FlowExecutionLifecycle
{
    /// <summary>The saved flow is being validated, compiled, and loaded.</summary>
    Preparing,

    /// <summary>The context is loaded and waiting for an execution command.</summary>
    Ready,

    /// <summary>The context is executing continuously.</summary>
    Running,

    /// <summary>The context is suspended at an execution boundary.</summary>
    Paused,

    /// <summary>The context is completing one requested execution step.</summary>
    Stepping,

    /// <summary>The context no longer matches the saved flow revision.</summary>
    Stale,

    /// <summary>The context stopped because execution failed.</summary>
    Faulted,

    /// <summary>The context has released its execution resources.</summary>
    Stopped
}