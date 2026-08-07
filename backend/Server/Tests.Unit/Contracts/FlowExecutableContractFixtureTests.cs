using System.Security.Cryptography;
using System.Text.Json;

namespace Tests.Unit.Contracts;

public sealed class FlowExecutableContractFixtureTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-executable-v1");

    [Test]
    public void ManifestDigestsAndExpectedResultsAreSelfConsistent()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot, "manifest.json")));
        var fixtures = manifest.RootElement.GetProperty("fixtures").EnumerateArray().ToArray();

        Assert.That(fixtures, Has.Length.EqualTo(8));
        foreach (var fixture in fixtures)
        {
            var id = fixture.GetProperty("id").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(FixtureRoot, id, "artifact.bin"));
            var expected = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(FixtureRoot, id, "expected-validation.json")));

            using (expected)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(bytes, Has.Length.EqualTo(fixture.GetProperty("artifactLength").GetInt32()));
                    Assert.That(Convert.ToHexStringLower(SHA256.HashData(bytes)),
                        Is.EqualTo(fixture.GetProperty("artifactSha256").GetString()));
                    Assert.That(expected.RootElement.GetProperty("reasonCode").GetInt32(),
                        Is.EqualTo(fixture.GetProperty("expected").GetProperty("reasonCode").GetInt32()));
                    Assert.That(expected.RootElement.GetProperty("path").GetString(),
                        Is.EqualTo(fixture.GetProperty("expected").GetProperty("path").GetString()));
                });
            }
        }
    }

    [Test]
    public void SourceOrderingDoesNotChangeCanonicalArtifact()
    {
        var canonical = File.ReadAllBytes(Path.Combine(FixtureRoot, "valid-two-button-and", "artifact.bin"));
        var permuted = File.ReadAllBytes(Path.Combine(FixtureRoot, "valid-source-order-permutation", "artifact.bin"));

        Assert.That(permuted, Is.EqualTo(canonical));
    }

    [Test]
    public void StableNodeIdsRoundTripIntoExpectedSnapshots()
    {
        using var decoded = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(FixtureRoot, "valid-two-button-and", "decoded.json")));
        using var snapshots = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(FixtureRoot, "valid-two-button-and", "expected-snapshots.json")));
        var decodedIds = decoded.RootElement.GetProperty("nodeIds").EnumerateArray()
            .Select(value => value.GetString()).ToHashSet(StringComparer.Ordinal);

        foreach (var snapshot in snapshots.RootElement.EnumerateArray())
        {
            var snapshotIds = snapshot.GetProperty("nodes").EnumerateArray()
                .Select(node => node.GetProperty("nodeId").GetString()).ToHashSet(StringComparer.Ordinal);
            Assert.That(snapshotIds, Is.EqualTo(decodedIds));
        }
    }
}
