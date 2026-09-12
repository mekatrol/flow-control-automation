namespace Server.Common.Types;

/// <summary>Identifies a stable category of template failure.</summary>
public enum TemplateError
{
    InvalidTemplate,
    MissingValue,
    UnsupportedValue,
    ExecutionLimitExceeded,
    OutputLimitExceeded,
    RenderFailed
}