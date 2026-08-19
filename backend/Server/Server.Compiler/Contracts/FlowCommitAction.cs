namespace Server.Compiler.Contracts;

/// <summary>
/// Identifies an operation in the Flow IL commit-plan section.
/// Values correspond directly to the encoded commit-action byte.
/// </summary>
internal enum FlowCommitAction : byte
{
    /// <summary>
    /// Copies a node's staged value into its persistent state slot at the end
    /// of the current scan.
    /// </summary>
    StateCommit = 1
}