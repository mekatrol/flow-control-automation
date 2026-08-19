using Server.Common.Contracts;

namespace Server.Api.Contracts;

/// <summary>Defines the immutable flow and execution host used to create a debug session.</summary>
public sealed record CreateDebugSessionRequest
{
    /// <summary>Gets the complete flow snapshot to compile; it must use the current contract version and pass backend flow validation.</summary>
    public required ExecutableFlowSource Source { get; init; }

    /// <summary>Gets whether a current session for the same flow may be stopped and atomically replaced; otherwise creation reports a conflict.</summary>
    public bool ReplaceExisting { get; init; }

    /// <summary>Gets the execution-host vocabulary value; supported values are <c>controller</c>, <c>server</c>, and <c>emulator</c>.</summary>
    public string Host { get; init; } = "controller";

    /// <summary>Gets the emulator session identifier when <see cref="Host"/> is <c>emulator</c>; it must be a live emulator ID in that mode and <see langword="null"/> otherwise.</summary>
    public string? EmulatorId { get; init; }
}