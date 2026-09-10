namespace Server.Common.Types;

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
    InvalidShape
}