using Server.Common.Contracts.Templating;

namespace Tests.Unit.Templating;

public sealed class TemplateServiceTests
{
    [Test]
    public void AddServerServices_RegistersSingletonTemplateService()
    {
        using var provider = Helpers.TestServices.CreateProvider();

        var first = provider.GetRequiredService<ITemplateService>();
        var second = provider.GetRequiredService<ITemplateService>();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void Validate_InvalidTemplateReturnsSourceDiagnostic()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        var result = service.Validate("{{ if value }}");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(result.Diagnostics[0].Category, Is.EqualTo(TemplateError.InvalidTemplate));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(1));
        });
    }

    [Test]
    public void Render_UsesValuesAndJsonSerialization()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();
        var values = new Dictionary<string, object?>
        {
            ["value"] = "open\"closed"
        };

        var result = service.Render("{\"value\":{{ value | json }}}", values);

        Assert.That(result, Is.EqualTo("{\"value\":\"open\\u0022closed\"}"));
    }

    [Test]
    public void Render_MissingValueThrowsStableCategory()
    {
        using var provider = Helpers.TestServices.CreateProvider();
        var service = provider.GetRequiredService<ITemplateService>();

        var exception = Assert.Throws<TemplateRenderException>(
            () => service.Render("{{ missing }}", new Dictionary<string, object?>()));

        Assert.That(exception!.Category, Is.EqualTo(TemplateError.MissingValue));
    }
}