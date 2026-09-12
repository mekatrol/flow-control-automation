using System.Text;

namespace Tests.Unit.Points.Validation;

public sealed class PointSourceAggregateTests
{
    [TestCase("virtual")]
    [TestCase("physical")]
    [TestCase("home-assistant")]
    [TestCase("mqtt")]
    [TestCase("http-json")]
    public void CanonicalFixture_RoundTripsAndValidates(string name)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "ContractFixtures", "point-sources", "valid", $"{name}.v1.yaml");
        var source = PointSourceYaml.Parse(File.ReadAllBytes(path));

        var rendered = PointSourceYaml.Render(source);
        var restored = PointSourceYaml.Parse(Encoding.UTF8.GetBytes(rendered));

        Assert.That(PointSourceYaml.Render(restored), Is.EqualTo(rendered));
    }

    [TestCase("mapping")]
    [TestCase("mapping/alias/extra")]
    [TestCase("/alias")]
    [TestCase("mapping/")]
    public void Resolver_RejectsMalformedPath(string path)
    {
        var source = Source(path);
        Assert.Throws<ArgumentException>(() =>
            new PointMappingResolver().Resolve(source, source.Points[0]));
    }

    [Test]
    public void Resolver_UsesCaseSensitiveAliasAndReturnsAggregateMembers()
    {
        var source = Source("mapping/valueAlias");
        var result = new PointMappingResolver().Resolve(source, source.Points[0]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Source, Is.SameAs(source));
            Assert.That(result.Mapping, Is.SameAs(source.Mappings[0]));
            Assert.That(result.Alias, Is.EqualTo("valueAlias"));
        });
    }

    [TestCase("schemaVersion: 1\nsources: []")]
    [TestCase("schemaVersion: 1\npoints: []")]
    public void Parser_RejectsRemovedRootShapes(string yaml) =>
        Assert.Throws<ConfigurationYamlException>(() => PointSourceYaml.Parse(Encoding.UTF8.GetBytes(yaml)));

    private static PointSource Source(string path) => new()
    {
        Id = "source",
        Name = "Source",
        Enabled = true,
        Kind = PointSourceKind.Virtual,
        Mappings = [new PointMapping { Id = "mapping", Aliases = ["valueAlias"], Read = new(), Virtual = new() }],
        Points = [new AutomationPoint
        {
            Id = "point", Name = "Point", Enabled = true, Direction = DataDirectionType.Value,
            ValueType = AutomationPointValueType.Text, Readable = true, Persistence = "volatile",
            Limits = new() { ["maximumLength"] = 20 }, Mapping = path
        }]
    };
}