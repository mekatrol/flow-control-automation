namespace Tests.Unit.Templating;

internal sealed class TemplateFixtureException(string message, Exception? innerException = null)
    : Exception(message, innerException);