using Server.Common.Models;

namespace Server.Api.Contracts;

/// <summary>Defines the immutable executable flow used to create an emulator instance.</summary>
public sealed record CreateFlowEmulatorRequest
{
    /// <summary>Gets the complete flow snapshot to compile and emulate; it must use the current supported contract version and pass backend validation.</summary>
    public required ExecutableFlowSource Source { get; init; }
}