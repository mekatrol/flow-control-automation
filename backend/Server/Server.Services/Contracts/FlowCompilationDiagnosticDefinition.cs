namespace Server.Services.Contracts;

/// <summary>
/// <para>Describes one stable diagnostic.</para>
/// <para>
/// <paramref name="Title"/> is the short, UI-friendly description.
/// <paramref name="MessageFormat"/> is the longer invariant fallback message and
/// may contain composite-format placeholders such as {0}.
/// </para>
/// <para>
/// Resource keys are deliberately stable and can later be backed by RESX,
/// IStringLocalizer, a database, or another localization mechanism.
/// </para>
/// </summary>
public sealed record FlowCompilationDiagnosticDefinition(
    FlowCompilationDiagnosticCode Code,
    string DisplayCode,
    string TitleResourceKey,
    string MessageResourceKey,
    string Title,
    string MessageFormat);
