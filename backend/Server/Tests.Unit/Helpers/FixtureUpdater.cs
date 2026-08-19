using Server.Common.Contracts;
using Server.Services.Implementation;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Tests.Unit.Helpers;

/// <summary>
/// Provides explicitly enabled regeneration of source-controlled test fixtures.
/// Fixture updates are controlled by <c>EnabledFixtures</c>.
/// </summary>
internal static class FixtureUpdater
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> EnabledFixtures = new(StringComparer.Ordinal)
    {
        // Canonical valid compiler fixtures.
        //"valid-two-button-and",
        //"valid-memory-feedback",
        //"valid-expanded-boolean",
        //"valid-numeric-language",
        //"valid-quality-timer-event",
        //"valid-analog-points",
        //"valid-source-order-permutation",
        //"maximum-boolean",

        // Deliberately invalid/corrupted contract fixtures.
        // Do not regenerate these as normal valid artifacts; their malformed bytes are intentional.

        //"malformed-truncated",
        //"invalid-operand",
        //"unknown-section",
        //"noncanonical-section-order",
    };

    /// <summary>
    /// Gets whether fixture regeneration is enabled for a specific fixture.
    /// </summary>
    public static bool IsEnabled(string fixture) => EnabledFixtures.Contains(fixture);

    /// <summary>
    /// Executes a fixture update only when regeneration is enabled for the specified fixture.
    /// </summary>
    public static void Update(string fixture, Action action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixture);
        ArgumentNullException.ThrowIfNull(action);

        if (IsEnabled(fixture))
        {
            action();
        }
    }

    /// <summary>
    /// Finds the project directory containing the calling source file.
    /// </summary>
    public static string ProjectDirectory(
        [CallerFilePath] string callerFilePath = "")
    {
        var directory = new DirectoryInfo(
            Path.GetDirectoryName(callerFilePath)
            ?? throw new InvalidOperationException("Unable to determine caller directory."));

        while (directory is not null)
        {
            if (directory.EnumerateFiles("*.csproj").Any())
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Unable to locate project directory from '{callerFilePath}'.");
    }

    /// <summary>
    /// Gets the workspace directory containing the project directory.
    /// </summary>
    public static string WorkspaceDirectory(
    [CallerFilePath] string callerFilePath = "")
    {
        var projectDirectory = new DirectoryInfo(ProjectDirectory(callerFilePath));

        return projectDirectory.Parent?.Parent?.Parent?.FullName
            ?? throw new InvalidOperationException(
                $"Unable to determine workspace directory from project directory '{projectDirectory.FullName}'.");
    }

    public static void UpdateFlowCompilation(
        string fixture,
        FlowCompilationResult result,
        string fixtureRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixture);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureRoot);

        Update(fixture, () =>
        {
            var fixtureDirectory = Path.Combine(fixtureRoot, fixture);

            Directory.CreateDirectory(fixtureDirectory);

            FlowCompiler.WriteBinary(
                result,
                Path.Combine(fixtureDirectory, "artifact.bin"));

            FlowCompiler.WriteIntelHex(
                result,
                Path.Combine(fixtureDirectory, "artifact.hex"));

            UpdateMetadata(
                fixture,
                result,
                fixtureDirectory);

            UpdateManifest(
                fixture,
                result,
                fixtureRoot);
        });
    }

    private static void UpdateMetadata(string fixture, FlowCompilationResult result, string fixtureDirectory)
    {
        var metadata = new
        {
            flowId = fixture,
            flowRevision = result.FlowRevision,
            sectionCount = result.SectionCount,
            instructionCount = result.InstructionCount,
            slotCount = result.SlotCount,
            pointCount = result.PointCount,
            stateCount = result.StateCount,
            schedule = result.Schedule,
            slots = result.NodeIndices,
            artifactLength = result.Artifact.Length
        };

        File.WriteAllText(
            Path.Combine(fixtureDirectory, "metadata.json"),
            JsonSerializer.Serialize(
                metadata,
                serializerOptions));
    }

    private static void UpdateManifest(
        string fixture,
        FlowCompilationResult result,
        string fixtureRoot)
    {
        var manifestPath = Path.Combine(
            fixtureRoot,
            "manifest.json");

        using var document = JsonDocument.Parse(
            File.ReadAllText(manifestPath));

        var root = document.RootElement;

        var fixtures = root.GetProperty("fixtures")
            .EnumerateArray()
            .Select(item =>
            {
                var id = item.GetProperty("id").GetString()!;

                if (!string.Equals(id, fixture, StringComparison.Ordinal))
                {
                    return JsonSerializer.Deserialize<object>(
                        item.GetRawText())!;
                }

                return new
                {
                    id,
                    artifactLength = result.Artifact.Length,
                    artifactSha256 = result.ArtifactSha256,
                    expected = JsonSerializer.Deserialize<object>(
                        item.GetProperty("expected").GetRawText())!
                };
            })
            .ToArray();

        var manifest = new
        {
            contract = root.GetProperty("contract").GetString(),
            fixtures
        };

        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(
                manifest,
                serializerOptions));
    }
}