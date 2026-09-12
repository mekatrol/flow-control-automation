namespace Tests.Unit.Templating;

public sealed record TemplateFixture
{
    public string? Name { get; init; }

    public string? Template { get; init; }

    public Dictionary<string, object?> Values { get; init; } = [];

    public string? Expected { get; init; }

    public TemplateError? ExpectedError { get; init; }
}