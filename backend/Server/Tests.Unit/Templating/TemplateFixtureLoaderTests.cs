namespace Tests.Unit.Templating;

[TestFixture]
public sealed class TemplateFixtureLoaderTests
{
    private static string FixtureRoot => Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "TemplateFixtures");

    [Test]
    public void LoadDirectory_DiscoversValidFixturesDeterministically()
    {
        var positive = TemplateFixtureLoader.LoadDirectory(Path.Combine(FixtureRoot, "positive"));
        var negative = TemplateFixtureLoader.LoadDirectory(Path.Combine(FixtureRoot, "negative"));

        Assert.Multiple(() =>
        {
            Assert.That(positive, Has.Count.EqualTo(9));
            Assert.That(negative, Has.Count.EqualTo(6));
            Assert.That(
                positive.Select(value => value.Name),
                Is.EqualTo(positive.Select(value => value.Name).Order(StringComparer.Ordinal)));
            Assert.That(
                negative.Select(value => value.Name),
                Is.EqualTo(negative.Select(value => value.Name).Order(StringComparer.Ordinal)));
        });
    }

    [Test]
    public void Load_PreservesMultilineTemplateExactly()
    {
        var fixture = TemplateFixtureLoader.Load(
            Path.Combine(FixtureRoot, "positive", "04-mqtt-json-payload.yaml"));

        Assert.That(
            fixture.Template,
            Is.EqualTo("{\n  \"value\": {{ value | to_json }},\n  \"source\": {{ source | to_json }}\n}"));
    }

    [TestCase("syntax.yaml")]
    [TestCase("duplicate-key.yaml")]
    [TestCase("missing-name.yaml")]
    [TestCase("missing-template.yaml")]
    [TestCase("both-outcomes.yaml")]
    [TestCase("neither-outcome.yaml")]
    public void Load_RejectsMalformedFixture(string file)
    {
        Assert.Throws<TemplateFixtureException>(() => TemplateFixtureLoader.Load(
            Path.Combine(FixtureRoot, "malformed", file)));
    }

    [Test]
    public void LoadDirectory_RejectsDuplicateFixtureNames()
    {
        Assert.Throws<TemplateFixtureException>(() => TemplateFixtureLoader.LoadDirectory(
            Path.Combine(FixtureRoot, "duplicate-names")));
    }
}