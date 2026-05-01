using System.Xml.Linq;

namespace CrossMacro.Core.Tests.Architecture;

public class ArchitectureBoundaryTests
{
    private static readonly string[] CoreForbiddenNamespaces =
    [
        "CrossMacro.Platform.Abstractions",
        "CrossMacro.Daemon.Contracts",
        "CrossMacro.Packaging.Abstractions",
        "CrossMacro.Infrastructure",
        "CrossMacro.UI",
        "CrossMacro.Cli",
        "CrossMacro.Platform.Linux",
        "CrossMacro.Platform.Windows",
        "CrossMacro.Platform.MacOS"
    ];

    private static readonly string[] PlatformAbstractionsForbiddenImplementationPatterns =
    [
        "Environment.GetEnvironmentVariable",
        "OperatingSystem.",
        "RuntimeInformation",
        "File.",
        "Directory."
    ];

    private static readonly string[] DaemonContractsForbiddenPatterns =
    [
        "CrossMacro.UI",
        "CrossMacro.Infrastructure",
        "CrossMacro.Platform.Linux",
        "CrossMacro.Platform.Windows",
        "CrossMacro.Platform.MacOS",
        "Microsoft.Extensions.DependencyInjection",
        "IServiceCollection",
        "ServiceCollection",
        "DependencyInjection",
        "RuntimeServiceCollectionExtensions"
    ];

    [Fact]
    public void CoreProject_ShouldNotReferenceOutwardProjects()
    {
        var projectReferences = ReadProjectReferenceNames("src/CrossMacro.Core/CrossMacro.Core.csproj");

        var violations = projectReferences
            .Where(IsForbiddenCoreProjectReference)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        AssertNoViolations(
            violations,
            "CrossMacro.Core is the strict inner core and must not reference Infrastructure, UI, CLI, Platform.*, Daemon*, or Packaging.Abstractions projects.");
    }

    [Fact]
    public void CoreSource_ShouldNotUseOutwardNamespaces()
    {
        var violations = FindTextViolations("src/CrossMacro.Core", CoreForbiddenNamespaces);

        AssertNoViolations(
            violations,
            "CrossMacro.Core source must not use outward platform, daemon, packaging, infrastructure, UI, or CLI namespaces. Move outward-facing ports or implementations outside Core instead.");
    }

    [Fact]
    public void PlatformAbstractionsProject_ShouldNotReferenceOtherProjects()
    {
        var projectReferences = ReadProjectReferenceNames("src/CrossMacro.Platform.Abstractions/CrossMacro.Platform.Abstractions.csproj");

        AssertNoViolations(
            projectReferences,
            "CrossMacro.Platform.Abstractions is an allow-list contract project and must not take ProjectReference dependencies.");
    }

    [Fact]
    public void PlatformAbstractionsSource_ShouldNotContainConcreteOsOrEnvironmentProbing()
    {
        var violations = FindTextViolations("src/CrossMacro.Platform.Abstractions", PlatformAbstractionsForbiddenImplementationPatterns);

        AssertNoViolations(
            violations,
            "CrossMacro.Platform.Abstractions may expose narrow contracts and value types only; concrete OS, filesystem, runtime, or environment probing belongs in platform/runtime implementations. IPlatformServiceRegistrar(IServiceCollection) remains allowed.");
    }

    [Fact]
    public void DaemonContractsSource_ShouldRemainWireOnly()
    {
        var violations = FindTextViolations("src/CrossMacro.Daemon.Contracts", DaemonContractsForbiddenPatterns);

        AssertNoViolations(
            violations,
            "CrossMacro.Daemon.Contracts is wire-only and must not reference UI, Infrastructure, concrete platform implementations, Microsoft DI, or runtime composition namespaces.");
    }

    [Fact]
    public void DaemonContractsProject_ShouldNotReferenceOtherProjects()
    {
        var projectReferences = ReadProjectReferenceNames("src/CrossMacro.Daemon.Contracts/CrossMacro.Daemon.Contracts.csproj");

        AssertNoViolations(
            projectReferences,
            "CrossMacro.Daemon.Contracts is wire-only and must not take ProjectReference dependencies.");
    }

    [Fact]
    public void PackagingAbstractionsProject_ShouldNotReferenceOtherProjects()
    {
        var projectReferences = ReadProjectReferenceNames("src/CrossMacro.Packaging.Abstractions/CrossMacro.Packaging.Abstractions.csproj");

        AssertNoViolations(
            projectReferences,
            "CrossMacro.Packaging.Abstractions is a quick-setup contract project and must not take ProjectReference dependencies.");
    }

    [Fact]
    public void CoreTestsProject_ShouldOnlyUseDocumentedCrossLayerTestReferences()
    {
        var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "CrossMacro.Core",
            "CrossMacro.Infrastructure",
            "CrossMacro.Daemon.Contracts",
            "CrossMacro.Platform.Abstractions"
        };

        var projectReferences = ReadProjectReferenceNames("tests/CrossMacro.Core.Tests/CrossMacro.Core.Tests.csproj");

        var violations = projectReferences
            .Where(reference => !allowedReferences.Contains(reference))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        AssertNoViolations(
            violations,
            "Core.Tests currently has intentional cross-layer test references for existing characterization coverage. Add new test dependencies to a matching mirrored test project or migrate these tests safely before expanding this exception list.");
    }

    private static void AssertNoViolations(IReadOnlyCollection<string> violations, string message)
    {
        Assert.True(
            violations.Count == 0,
            message + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static bool IsForbiddenCoreProjectReference(string projectName)
    {
        return projectName is "CrossMacro.Infrastructure"
            or "CrossMacro.UI"
            or "CrossMacro.Cli"
            or "CrossMacro.Daemon"
            or "CrossMacro.Daemon.Contracts"
            or "CrossMacro.Packaging.Abstractions"
            || projectName.StartsWith("CrossMacro.Platform.", StringComparison.Ordinal);
    }

    private static string[] ReadProjectReferenceNames(string projectPath)
    {
        var fullPath = Path.Combine(GetRepositoryRoot(), projectPath);
        var document = XDocument.Load(fullPath);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', Path.DirectorySeparatorChar)))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] FindTextViolations(string relativeDirectory, IReadOnlyCollection<string> forbiddenPatterns)
    {
        var root = GetRepositoryRoot();
        var directory = Path.Combine(root, relativeDirectory);

        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .SelectMany(path => FindTextViolationsInFile(root, path, forbiddenPatterns))
            .ToArray();
    }

    private static IEnumerable<string> FindTextViolationsInFile(string root, string path, IReadOnlyCollection<string> forbiddenPatterns)
    {
        var lines = File.ReadLines(path).Select((text, index) => (Number: index + 1, Text: text));
        var relativePath = Path.GetRelativePath(root, path);

        foreach (var line in lines)
        {
            foreach (var pattern in forbiddenPatterns)
            {
                if (line.Text.Contains(pattern, StringComparison.Ordinal))
                {
                    yield return $"{relativePath}:{line.Number}: contains '{pattern}'";
                }
            }
        }
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the CrossMacro repository root from the test output directory.");
    }
}
