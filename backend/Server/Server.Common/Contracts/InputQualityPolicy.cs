namespace Server.Common.Contracts;

/// <summary>
/// Defines how the VM handles input values whose runtime data quality
/// is not <see cref="DataQuality.Good"/>.
/// </summary>
public enum InputQualityPolicy : byte
{
    /// <summary>
    /// Requires runtime inputs to have <see cref="DataQuality.Good"/>
    /// quality. Execution fails when a required input is not good.
    /// </summary>
    RequireGood = 1,

    /// <summary>
    /// Allows non-good input quality to propagate through execution
    /// rather than rejecting the scan.
    /// </summary>
    Propagate = 2
}
