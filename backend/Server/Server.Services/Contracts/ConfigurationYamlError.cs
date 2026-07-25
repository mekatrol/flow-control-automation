namespace Server.Services.Contracts;

public enum ConfigurationYamlError
{
    Syntax,
    Empty,
    TooLarge,
    ExcessiveNesting,
    UnsupportedFeature,
    MultipleDocuments,
    UnsupportedSchema,
    UnknownField,
    InvalidShape,
}