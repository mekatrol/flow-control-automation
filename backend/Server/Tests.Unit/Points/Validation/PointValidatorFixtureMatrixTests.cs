using Server.Services;
using Tests.Unit.Helpers;

namespace Tests.Unit.Points.Validation;

public sealed class PointValidatorFixtureMatrixTests
{
    private const string FixtureDirectoryName = "matrix";
    private const string ExpectationFileName = "expectations.csv";
    private static readonly string[] Kinds = ["virtual", "physical", "homeAssistant", "mqtt", "httpJson"];
    private static readonly string[] ValueTypes = ["analog", "digital", "multiState", "integer", "text"];

    [TestCaseSource(nameof(MatrixCases))]
    public void MatrixFixture_IsRejectedAtItsDeclaredBoundary(MatrixExpectation expectation)
    {
        using var provider = TestServices.CreateProvider();

        var source = PointSourceYaml.Parse(File.ReadAllBytes(expectation.FixturePath));
        var sourceValidator = provider.GetRequiredService<IPointSourceValidator>();
        var pointValidator = provider.GetRequiredService<IPointDefinitionValidator>();

        Assert.DoesNotThrow(() => sourceValidator.Validate(source));

        if (expectation.Outcome == "valid")
        {
            Assert.DoesNotThrow(() => pointValidator.Validate(
                source.Points.Single(), Context(source)));

            return;
        }

        var exception = Assert.Throws<PointDefinitionValidationException>(() =>
            pointValidator.Validate(source.Points.Single(), Context(source)));
        Assert.That(exception!.Message, Does.Contain(expectation.Diagnostic));
    }

    [Test]
    public void MatrixManifest_CoversEveryKindAndValueTypeInBothOutcomeSets()
    {
        var expectations = LoadExpectations();
        var expectedCells = Kinds.SelectMany(kind => ValueTypes.Select(valueType => (kind, valueType)))
            .ToHashSet();

        foreach (var outcome in new[] { "valid", "invalid" })
        {
            var actualCells = expectations.Where(item => item.Outcome == outcome)
                .Select(item => (item.Kind, item.ValueType)).ToHashSet();
            Assert.That(actualCells, Is.EquivalentTo(expectedCells), outcome);
        }
    }

    [Test]
    public void MatrixManifest_AndYamlDiscoveryAreOneToOne()
    {
        var directory = MatrixDirectory();
        var declared = LoadExpectations().Select(item => Path.GetFileName(item.FixturePath)).ToArray();
        var discovered = Directory.GetFiles(directory, "*.yaml").Select(Path.GetFileName).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(declared, Is.Unique);
            Assert.That(declared, Is.EquivalentTo(discovered));
            Assert.That(LoadExpectations(), Has.All.Matches<MatrixExpectation>(item => item.Outcome == "valid" || !string.IsNullOrWhiteSpace(item.Diagnostic)));
        }
    }

    [TestCaseSource(nameof(CombinedAggregateCases))]
    public void CombinedAggregateFixture_ValidatesEveryValueTypeAcrossMultipleMappings(
        string fixturePath)
    {
        using var provider = TestServices.CreateProvider();

        var source = PointSourceYaml.Parse(File.ReadAllBytes(fixturePath));
        var sourceValidator = provider.GetRequiredService<IPointSourceValidator>();
        var pointValidator = provider.GetRequiredService<IPointDefinitionValidator>();
        var expectedTypes = Enum.GetValues<AutomationPointValueType>();

        Assert.DoesNotThrow(() => sourceValidator.Validate(source));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.Mappings, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(source.Points.Select(point => point.ValueType), Is.EquivalentTo(expectedTypes));
            Assert.That(source.Points.Select(point => point.Mapping.Split('/')[0]).Distinct().ToArray(), Has.Length.GreaterThanOrEqualTo(2));

            foreach (var point in source.Points)
            {
                Assert.DoesNotThrow(() => pointValidator.Validate(point, Context(source)), $"{source.Kind}/{point.ValueType}");
            }
        }
    }

    [Test]
    public void CombinedAggregateFixtures_CoverEverySourceKindExactlyOnce()
    {
        var sources = CombinedAggregateCases()
            .Select(path => PointSourceYaml.Parse(File.ReadAllBytes(path))).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sources, Has.Length.EqualTo(Kinds.Length));
            Assert.That(sources.Select(source => source.Kind), Is.EquivalentTo(Enum.GetValues<PointSourceKind>()));
            Assert.That(sources.Select(source => source.Kind), Is.Unique);
        }
    }

    [TestCase(99, "timeouts.connectMilliseconds")]
    [TestCase(100, null)]
    [TestCase(30000, null)]
    [TestCase(30001, "timeouts.connectMilliseconds")]
    public void SourceValidator_EnforcesConnectTimeoutBoundaries(int milliseconds, string? diagnostic)
    {
        var source = ValidSource() with { Timeouts = new() { ConnectMilliseconds = milliseconds } };
        AssertSourceOutcome(source, diagnostic);
    }

    [TestCase(0, "maximumResponseBytes")]
    [TestCase(1, null)]
    [TestCase(10485760, null)]
    [TestCase(10485761, "maximumResponseBytes")]
    public void SourceValidator_EnforcesResponseSizeBoundaries(int bytes, string? diagnostic)
    {
        var source = ValidSource() with
        {
            Connection = ValidSource().Connection with { MaximumResponseBytes = bytes }
        };
        AssertSourceOutcome(source, diagnostic);
    }

    [Test]
    public void SourceValidator_EnforcesGlobalPointIdentity()
    {
        var source = ValidSource();
        var other = source with { Id = "other-source" };
        using var provider = TestServices.CreateProvider();
        var validator = provider.GetRequiredService<IPointSourceValidator>();

        var exception = Assert.Throws<PointSourceValidationException>(() =>
            validator.Validate(source, [other]));
        Assert.That(exception!.Message, Does.Contain("globally unique"));
    }

    [TestCase("duplicate-alias.yaml", "mapping aliases")]
    [TestCase("duplicate-mapping-id.yaml", "duplicate mapping id")]
    [TestCase("duplicate-point-id.yaml", "duplicate point id")]
    [TestCase("incompatible-capability.yaml", "requires a read operation")]
    [TestCase("malformed-template-output.yaml", "template output is malformed")]
    [TestCase("unknown-alias.yaml", "unknown alias")]
    [TestCase("unknown-mapping.yaml", "unknown mapping")]
    public void PhaseOneSemanticInvalidFixture_IsRejectedBySourceValidator(
        string fixture, string diagnostic)
    {
        var source = PointSourceYaml.Parse(File.ReadAllBytes(InvalidFixturePath(fixture)));
        using var provider = TestServices.CreateProvider();

        var exception = Assert.Throws<PointSourceValidationException>(() =>
            provider.GetRequiredService<IPointSourceValidator>().Validate(source));
        Assert.That(exception!.Message, Does.Contain(diagnostic));
    }

    [TestCase("inline-mapping.yaml")]
    [TestCase("old-sources-wrapper.yaml")]
    [TestCase("point-source-type.yaml")]
    [TestCase("source-id.yaml")]
    [TestCase("standalone-points.yaml")]
    [TestCase("unknown-field.yaml")]
    [TestCase("unsupported-schema.yaml")]
    public void PhaseOneStructuralInvalidFixture_IsRejectedByStrictParser(string fixture) =>
        Assert.Throws<ConfigurationYamlException>(() =>
            PointSourceYaml.Parse(File.ReadAllBytes(InvalidFixturePath(fixture))));

    [TestCase("Bad_Id", "id must be")]
    [TestCase("", "id must be")]
    public void SourceValidator_RejectsInvalidIdentifiers(string id, string diagnostic) =>
        AssertSourceOutcome(ValidSource() with { Id = id }, diagnostic);

    [TestCase("ftp://api.example.test", true, "scheme")]
    [TestCase("https://user:password@api.example.test", true, "must not contain credentials")]
    [TestCase("https://api.example.test", false, "certificate verification")]
    public void SourceValidator_RejectsUnsafeUrlsAndTls(
        string baseUrl, bool verifyCertificate, string diagnostic)
    {
        var baseline = ValidSource();
        var source = baseline with
        {
            Connection = baseline.Connection with { BaseUrl = baseUrl },
            Tls = new() { VerifyServerCertificate = verifyCertificate }
        };

        AssertSourceOutcome(source, diagnostic);
    }

    [TestCase(-1, "qos")]
    [TestCase(0, null)]
    [TestCase(2, null)]
    [TestCase(3, "qos")]
    public void SourceValidator_EnforcesMqttQosBoundaries(int qos, string? diagnostic)
    {
        var baseline = ValidSource();
        var source = baseline with
        {
            Kind = PointSourceKind.Mqtt,
            Connection = new()
            {
                BrokerUrl = "mqtts://mqtt.example.test",
                Qos = qos,
                TestTopic = "fixture/state"
            },
            Mappings = [baseline.Mappings[0] with
            {
                Read = new() { Topic = "fixture/state", Template = "{ \"value\": 1 }" }
            }]
        };

        AssertSourceOutcome(source, diagnostic);
    }

    [TestCase("values", "mapping reference")]
    [TestCase("values/value/extra", "mapping reference")]
    public void PointDefinitionValidator_RejectsMalformedOrCaseMismatchedBindings(
        string mapping, string diagnostic)
    {
        var source = ValidSource();
        var point = source.Points[0] with { Mapping = mapping };
        source = source with { Points = [point] };
        using var provider = TestServices.CreateProvider();

        var exception = Assert.Throws<PointDefinitionValidationException>(() =>
            provider.GetRequiredService<IPointDefinitionValidator>().Validate(point, Context(source)));
        Assert.That(exception!.Message, Does.Contain(diagnostic));
    }

    private static MatrixExpectation[] MatrixCases() => LoadExpectations();

    private static string[] CombinedAggregateCases() =>
        Directory.GetFiles(CombinedAggregateDirectory(), "*.yaml");

    private static MatrixExpectation[] LoadExpectations()
    {
        var path = Path.Combine(MatrixDirectory(), ExpectationFileName);

        return [.. File.ReadAllLines(path).Skip(1).Where(line => line.Length > 0).Select(line =>
        {
            var fields = line.Split(',', 6);

            return new MatrixExpectation(
                Path.Combine(MatrixDirectory(), fields[0]), fields[1], fields[2],
                fields[3], fields[4], fields[5]);
        })];
    }

    private static string MatrixDirectory() => Path.Combine(
        TestContext.CurrentContext.TestDirectory, "ContractFixtures", "point-sources",
        "validation", FixtureDirectoryName);

    private static string CombinedAggregateDirectory() => Path.Combine(
        TestContext.CurrentContext.TestDirectory, "ContractFixtures", "point-sources",
        "validation", "combinations");

    private static string InvalidFixturePath(string fixture) => Path.Combine(
        TestContext.CurrentContext.TestDirectory, "ContractFixtures", "point-sources",
        "invalid", fixture);

    private static PointValidationContext Context(PointSource source) =>
        new(new Dictionary<string, PointSource> { [source.Id] = source });

    private static void AssertSourceOutcome(PointSource source, string? diagnostic)
    {
        using var provider = TestServices.CreateProvider();
        var validator = provider.GetRequiredService<IPointSourceValidator>();

        if (diagnostic is null)
        {
            Assert.DoesNotThrow(() => validator.Validate(source));

            return;
        }

        var exception = Assert.Throws<PointSourceValidationException>(() => validator.Validate(source));
        Assert.That(exception!.Message, Does.Contain(diagnostic));
    }

    private static PointSource ValidSource() => new()
    {
        Id = "source",
        Name = "Source",
        Enabled = true,
        Kind = PointSourceKind.HttpJson,
        Connection = new()
        {
            BaseUrl = "https://api.example.test",
            MaximumResponseBytes = 1024
        },
        Tls = new() { VerifyServerCertificate = true },
        Timeouts = new() { ConnectMilliseconds = 100 },
        Mappings = [new PointMapping
        {
            Id = "values",
            Aliases = ["value"],
            Read = new() { Path = "/value", Method = "GET", Template = "{ \"value\": 1 }" }
        }],
        Points = [new AutomationPoint
        {
            Id = "point",
            Name = "Point",
            Enabled = true,
            Direction = DataDirectionType.Input,
            ValueType = AutomationPointValueType.Analog,
            Readable = true,
            Persistence = "volatile",
            Mapping = "values/value"
        }]
    };

    public sealed record MatrixExpectation(
        string FixturePath,
        string Kind,
        string ValueType,
        string Outcome,
        string Boundary,
        string Diagnostic)
    {
        public override string ToString() => System.IO.Path.GetFileNameWithoutExtension(FixturePath);
    }
}