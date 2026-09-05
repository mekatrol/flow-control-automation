using Server.Common.Models;

namespace Server.Compiler.Contracts;

/// <summary>
/// Controller metadata resolved before compilation begins.
/// </summary>
public sealed record FlowCompilationTarget
{
    public required ValidatedControllerTemplate ControllerTemplate { get; init; }
    public IReadOnlyList<AutomationPoint> Points { get; init; } = [];
}