namespace Server.Common.Contracts.Templating;

/// <summary>Specifies how values emitted by a template are encoded.</summary>
public enum RenderAs
{
    /// <summary>Writes values using template engine's invariant plain-text formatting.</summary>
    Text,

    /// <summary>Writes every emitted value as a complete JSON value and validates the final JSON document.</summary>
    Json
}