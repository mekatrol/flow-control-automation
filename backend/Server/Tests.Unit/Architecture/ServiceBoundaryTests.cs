using Server.Services;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ServerServiceCollectionExtensions = Server.Services.ServiceExtensions.ServiceCollectionExtensions;

namespace Tests.Unit.Architecture;

public sealed partial class ServiceBoundaryTests
{
    private static readonly string ServerDirectory = FindServerDirectory();

    [Test]
    public void ServicesExposeOnlyTheApprovedCompositionAndErrorSurface()
    {
        var approved = new HashSet<Type>
        {
            typeof(ServerServiceCollectionExtensions),
            typeof(ServerOptions),
            typeof(ControllerGatewayException),
            typeof(ControllerTemplateConflictException),
            typeof(ControllerTemplateValidationException),
            typeof(CredentialConflictException),
            typeof(CredentialNotFoundException),
            typeof(CredentialResolutionException),
            typeof(CredentialValidationException),
            typeof(ExecutionConfigurationException),
            typeof(FlowConcurrencyException),
            typeof(FlowDebugSessionNotFoundException),
            typeof(FlowEmulatorNotFoundException),
            typeof(FlowNotFoundException),
            typeof(FlowSimulatorException),
            typeof(FlowSimulatorOptions),
            typeof(FlowVmException),
            typeof(PointDefinitionConflictException),
            typeof(PointDefinitionNotFoundException),
            typeof(PointDefinitionValidationException),
            typeof(PointSourceConflictException),
            typeof(PointSourceNotFoundException),
            typeof(PointSourceValidationException),
            typeof(VirtualPointWriterConflictException)
        };
        var actual = typeof(ServerServiceCollectionExtensions).Assembly.ExportedTypes.ToHashSet();

        Assert.That(actual, Is.EquivalentTo(approved));
    }

    [Test]
    public void CommonHasNoProjectDependencies()
    {
        var references = ProjectReferences("Server.Common");

        Assert.That(references, Is.Empty);
    }

    [Test]
    public void ApiAndTestsDoNotImportServiceImplementationNamespaces()
    {
        var offenders = SourceFiles("Server.Api", "Tests.Unit")
            .Where(file => File.ReadLines(file).Any(line => ServiceImplementationUsing().IsMatch(line)))
            .Select(RelativeToServer)
            .ToArray();

        Assert.That(offenders, Is.Empty);
    }

    [Test]
    public void ServiceTestsComposeThroughAddServerServices()
    {
        var helper = File.ReadAllText(Path.Combine(ServerDirectory, "Tests.Unit", "Helpers", "TestServices.cs"));
        var directServiceProviders = SourceFiles("Tests.Unit")
            .Where(file => !file.EndsWith(Path.Combine("Helpers", "TestServices.cs"), StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("new ServiceCollection()", StringComparison.Ordinal))
            .Select(RelativeToServer)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(helper, Does.Contain("AddServerServices(configuration)"));
            Assert.That(directServiceProviders, Is.EquivalentTo(new[]
            {
                Path.Combine("Tests.Unit", "Api", "RegistrationTests.cs"),
                Path.Combine("Tests.Unit", "Architecture", "ServiceBoundaryTests.cs"),
                Path.Combine("Tests.Unit", "Data", "DatabaseTests.cs"),
                Path.Combine("Tests.Unit", "Flows", "FlowCompilationTargetResolverTests.cs"),
                Path.Combine("Tests.Unit", "Flows", "FlowCompilerTests.cs"),
                Path.Combine("Tests.Unit", "Flows", "FlowDecompilerTests.cs"),
                Path.Combine("Tests.Unit", "Flows", "FlowServiceTests.cs")
            }), "Service tests must use the shared composition helper; only infrastructure and explicit composition-root tests may build their own collection.");
        });
    }

    [Test]
    public void ServicesHaveNoFriendAssembliesOrImplementationFolders()
    {
        var serviceDirectory = Path.Combine(ServerDirectory, "Server.Services");
        var friendDeclarations = SourceFiles("Server.Services")
            .Where(file => File.ReadAllText(file).Contains("InternalsVisibleTo", StringComparison.Ordinal))
            .Select(RelativeToServer)
            .ToArray();
        var implementationDirectories = Directory.EnumerateDirectories(serviceDirectory, "Implementation", SearchOption.AllDirectories)
            .Select(RelativeToServer)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(friendDeclarations, Is.Empty);
            Assert.That(implementationDirectories, Is.Empty);
        });
    }

    [Test]
    public void ServiceDomainFoldersMatchNamespaces()
    {
        var root = Path.Combine(ServerDirectory, "Server.Services");
        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Properties{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(file => (File: file, Relative: Path.GetRelativePath(root, file)))
            .Where(item => item.Relative.Contains(Path.DirectorySeparatorChar))
            .Where(item =>
            {
                var expected = $"namespace Server.Services.{Path.GetDirectoryName(item.Relative)!.Replace(Path.DirectorySeparatorChar, '.')};";

                return !File.ReadAllText(item.File).Contains(expected, StringComparison.Ordinal);
            })
            .Select(item => item.Relative)
            .ToArray();

        Assert.That(offenders, Is.Empty);
    }

    [Test]
    public void ServiceCrossDomainDependenciesFollowTheApprovedGraph()
    {
        var allowed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["Audit"] = [],
            ["Communication"] = [],
            ["Configuration"] = ["FlowExecution"],
            ["FlowExecution"] = ["Points"],
            ["Points"] = ["Communication", "Configuration"],
            ["Validation"] = []
        };
        var root = Path.Combine(ServerDirectory, "Server.Services");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            var sourceDomain = relative.Split(Path.DirectorySeparatorChar)[0];

            if (!allowed.TryGetValue(sourceDomain, out var targets))
            {
                continue;
            }

            foreach (var line in File.ReadLines(file))
            {
                var match = ServiceDomainUsing().Match(line);

                if (match.Success && match.Groups[1].Value != sourceDomain && !targets.Contains(match.Groups[1].Value))
                {
                    offenders.Add($"{relative}: {line.Trim()}");
                }
            }
        }

        Assert.That(offenders, Is.Empty);
    }

    [Test]
    public void ApiContractTypesAreNotReferencedByServiceDomains()
    {
        var offenders = SourceFiles("Server.Common", "Server.Services")
            .Where(file => File.ReadAllText(file).Contains("Server.Api.Contracts", StringComparison.Ordinal))
            .Select(RelativeToServer)
            .ToArray();

        Assert.That(offenders, Is.Empty);
    }

    private static IReadOnlyList<string> ProjectReferences(string projectName)
    {
        var project = XDocument.Load(Path.Combine(ServerDirectory, projectName, $"{projectName}.csproj"));

        return [.. project.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Cast<string>()];
    }

    private static IEnumerable<string> SourceFiles(params string[] projects) => projects
        .SelectMany(project => Directory.EnumerateFiles(Path.Combine(ServerDirectory, project), "*.cs", SearchOption.AllDirectories))
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string RelativeToServer(string path) => Path.GetRelativePath(ServerDirectory, path);

    private static string FindServerDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Server.slnx");

            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the backend Server solution directory.");
    }

    [GeneratedRegex(@"^\s*using\s+Server\.Services\.(?:Audit|Communication|Configuration|FlowExecution|Points|Validation)(?:\.|;)")]
    private static partial Regex ServiceImplementationUsing();

    [GeneratedRegex(@"^\s*using\s+Server\.Services\.(Audit|Communication|Configuration|FlowExecution|Points|Validation)(?:\.|;)")]
    private static partial Regex ServiceDomainUsing();
}