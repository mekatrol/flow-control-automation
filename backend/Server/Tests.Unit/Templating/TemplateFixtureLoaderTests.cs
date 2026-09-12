namespace Tests.Unit.Templating;

[Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
public sealed class TemplateFixtureLoaderTests
{
    private static string FixtureRoot => Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "TemplateFixtures");

    [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
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

    [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
    public void Load_PreservesMultilineTemplateExactly()
    {
        var fixture = TemplateFixtureLoader.Load(
            Path.Combine(FixtureRoot, "positive", "04-mqtt-json-payload.yaml"));

        Assert.That(
            fixture.Template,
            Is.EqualTo("{\n  \"value\": {{ value | json }},\n  \"source\": {{ source | json }}\n}"));
    }

    [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
    [Microsoft.VisualStudio.TestTools.UnitTesting.DataRow("syntax.yaml")]
    [Microsoft.VisualStudio.TestTools.UnitTesting.DataRow("duplicate-key.yaml")]
    [Microsoft.VisualStudio.TestTools.UnitTesting.DataRow("missing-name.yaml")]
    [Microsoft.VisualStudio.TestTools.UnitTesting.DataRow("missing-template.yaml")]
    [Microsoft.VisualStudio.TestTools.UnitTesting.DataRow("both-outcomes.yaml")]
    [Microsoft.VisualStudio.TestTools.UnitTesting.DataRow("neither-outcome.yaml")]
    public void Load_RejectsMalformedFixture(string file)
    {
        Assert.Throws<TemplateFixtureException>(() => TemplateFixtureLoader.Load(
            Path.Combine(FixtureRoot, "malformed", file)));
    }

    [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
    public void LoadDirectory_RejectsDuplicateFixtureNames()
    {
        Assert.Throws<TemplateFixtureException>(() => TemplateFixtureLoader.LoadDirectory(
            Path.Combine(FixtureRoot, "duplicate-names")));
    }
}