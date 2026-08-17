using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Runtime.CompilerServices;

namespace Tests.Unit.Helpers;

/// <summary>
/// Provides explicitly enabled regeneration of source-controlled test fixtures.
/// Fixture updates are controlled by <c>EnabledFixtures</c>.
/// </summary>
internal static class FixtureUpdater
{
    private static readonly HashSet<string> EnabledFixtures = new(StringComparer.Ordinal)
    {
        //"valid-memory-feedback"
        //"valid-two-button-and"
        "valid-two-button-and"
        //"valid-memory-feedback"
        //"valid-expanded-boolean"
        //"valid-numeric-language"
        //"valid-analog-points"
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

    public static void UpdateFlowCompilation(string fixture, FlowCompilationResult result, string fixtureRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixture);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureRoot);

        Update(fixture, () =>
        {
            var fixtureDirectory = Path.Combine(fixtureRoot, fixture);

            FlowCompiler.WriteBinary(
                result,
                Path.Combine(fixtureDirectory, "artifact.bin"));

            FlowCompiler.WriteIntelHex(
                result,
                Path.Combine(fixtureDirectory, "artifact.hex"));
        });
    }
}