namespace Server.Services.Contracts;

/// <summary>
/// Controller metadata resolved before compilation begins.
/// </summary>
public sealed record FlowCompilationTarget
{
    public required ValidatedControllerTemplate ControllerTemplate { get; init; }
    public IReadOnlyList<Point> Points { get; init; } = [];
}