namespace Server.Common.Models.FlowExecution;

/// <summary>Provides labels and safety metadata without controlling operation dispatch.</summary>
public sealed record FlowExecutionPresentation
{
    public string ModeLabel { get; init; } = string.Empty;
    public string HostLabel { get; init; } = string.Empty;
    public bool IsSimulated { get; init; }
    public bool UsesPhysicalIo { get; init; }
}