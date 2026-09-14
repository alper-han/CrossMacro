using System.Globalization;
using System.Reflection;
using System.Collections.ObjectModel;

namespace CrossMacro.Core.Tests.Architecture;

public sealed partial class ArchitectureBoundaryTests
{
    [GeneratedRegex(
        @"(?<![A-Za-z0-9_])(?:global::)?(CrossMacro\.Infrastructure(?:\.[A-Za-z_][A-Za-z0-9_]*)*)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex InfrastructureReferenceRegex { get; }

    private static readonly char[] PathSeparators =
    [
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar,
    ];

    private static readonly IReadOnlyDictionary<string, string[]> TemporaryPlatformInfrastructureProjectReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    private static readonly string[] TemporaryPlatformInfrastructureSourceFiles =
    [];

    private static readonly string[] CoreForbiddenNamespaces =
    [
        "CrossMacro.Platform.Abstractions",
        "CrossMacro.Daemon.Contracts",
        "CrossMacro.Packaging.Abstractions",
        "CrossMacro.Infrastructure",
        "CrossMacro.Application",
        "CrossMacro.UI",
        "CrossMacro.Cli",
        "CrossMacro.Platform.Linux",
        "CrossMacro.Platform.Windows",
        "CrossMacro.Platform.MacOS",
        "CrossMacro.Mcp",
        "Environment.GetEnvironmentVariable",
        "Environment.GetFolderPath",
        "OperatingSystem.",
    ];

    private static readonly string[] PlatformAbstractionsForbiddenImplementationPatterns =
    [
        "Environment.GetEnvironmentVariable",
        "OperatingSystem.",
        "RuntimeInformation",
        "File.",
        "Directory.",
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
        "RuntimeServiceCollectionExtensions",
    ];

    [Fact]
    public void CoreProject_ShouldNotReferenceOutwardProjects()
    {
        var projectReferences = ReadProjectReferenceNames("src/CrossMacro.Core/CrossMacro.Core.csproj");

        AssertNoViolations(
            projectReferences,
            "CrossMacro.Core is the strict inner core and must not take any project reference beyond the .NET BCL.");

        AssertNoViolations(
            ReadNonProjectDependencies("src/CrossMacro.Core/CrossMacro.Core.csproj"),
            "CrossMacro.Core must use only the .NET BCL and must not add package or framework dependencies.");
    }

    [Fact]
    public void ApplicationProject_ShouldReferenceOnlyCore()
    {
        var projectReferences = ReadProjectReferenceNames("src/CrossMacro.Application/CrossMacro.Application.csproj");

        Assert.Equal(["CrossMacro.Core"], projectReferences);
    }

    [Fact]
    public void ApplicationProject_ShouldNotReferenceFrameworkOrOsAdapters()
    {
        var dependencies = ReadProjectDependencies("src/CrossMacro.Application/CrossMacro.Application.csproj");
        var violations = dependencies
            .Where(dependency => !(
                (dependency.Kind is "ProjectReference" && GetDependencyName(dependency) is "CrossMacro.Core")
                || (dependency.Kind is "PackageReference" && GetDependencyName(dependency) is "Microsoft.Extensions.DependencyInjection.Abstractions")))
            .Select(dependency => $"{dependency.Kind}: {GetDependencyName(dependency)}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        AssertNoViolations(
            violations,
            "Application may reference Core and the DI registration abstractions, never host implementations or a container runtime.");

        AssertNoViolations(
            FindProjectTextViolations(
                "src/CrossMacro.Application/CrossMacro.Application.csproj",
                [
                    "Avalonia",
                    "CrossMacro.Infrastructure",
                    "CrossMacro.Platform.",
                    "CrossMacro.Mcp",
                    "Environment.",
                    "OperatingSystem",
                    "RuntimeInformation",
                ]),
            "CrossMacro.Application project metadata must not mention concrete adapters, host frameworks, or OS/runtime APIs.");
    }

    [Fact]
    public void ApplicationSource_ShouldNotDependOnConcreteAdaptersOrHostFrameworks()
    {
        var violations = FindTextViolations(
            "src/CrossMacro.Application",
            [
                "CrossMacro.Infrastructure",
                "CrossMacro.Platform.Linux",
                "CrossMacro.Platform.Windows",
                "CrossMacro.Platform.MacOS",
                "CrossMacro.Mcp",
                "Avalonia",
                "Environment.",
                "OperatingSystem.",
                "RuntimeInformation",
            ]);

        AssertNoViolations(
            violations,
            "CrossMacro.Application source must expose ports and results, not concrete adapters, host frameworks, or runtime composition.");
    }

    [Fact]
    public void ApplicationBusinessCode_ShouldNotResolveServicesOrRegisterDependencies()
    {
        var directory = Path.Combine(GetRepositoryRoot(), "src", "CrossMacro.Application");
        var businessSources = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(PathSeparators).Any(segment => segment is "obj" or "bin" or "DependencyInjection"));
        AssertNoViolations(
            businessSources.SelectMany(path => FindTextViolationsInFile(path,
                ["Microsoft.Extensions.DependencyInjection", "IServiceProvider", "IServiceCollection", "GetRequiredService", "GetService"])).ToArray(),
            "Application business code must receive explicit ports; only the registration module may use DI abstractions.");
        AssertNoViolations(
            FindTextViolations("src/CrossMacro.Application", ["IServiceProvider", "BuildServiceProvider"]),
            "Application registrations must not create a container or hide service resolution in business objects.");
    }

    [Fact]
    public void ApplicationTaskPorts_ShouldNotExposeUiCollectionsOrRuntimeLifecycle()
    {
        var ports = new[]
        {
            typeof(IScheduledTaskOperations),
            typeof(IShortcutTaskOperations),
            typeof(ITriggerTaskOperations),
        };

        foreach (var port in ports)
        {
            var declaredMembers = port.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.DoesNotContain(declaredMembers, member => member is EventInfo);
            Assert.DoesNotContain(declaredMembers, member => member.Name is "Start" or "Stop" or "StopAsync" or "Completion");

            foreach (var property in port.GetProperties())
            {
                var isObservableCollection = property.PropertyType.IsGenericType
                    && property.PropertyType.GetGenericTypeDefinition() == typeof(ObservableCollection<>);
                Assert.False(isObservableCollection, $"{port.Name}.{property.Name} must not expose ObservableCollection.");
            }
        }

        Assert.DoesNotContain(typeof(IScheduledTaskOperations).GetMembers(), member => member.Name is "LoadAsync" or "SaveAsync" or "Tasks");
        Assert.DoesNotContain(typeof(IShortcutTaskOperations).GetMembers(), member => member.Name is "LoadAsync" or "SaveAsync" or "Tasks");
        Assert.DoesNotContain(typeof(ITriggerTaskOperations).GetMembers(), member => member.Name is "LoadAsync" or "SaveAsync" or "Tasks");
    }

    [Fact]
    public void UiAndCliInfrastructureReferences_ShouldBeAbsentFromHostLibraries()
    {
        var hostProjects = Directory.EnumerateFiles(Path.Combine(GetRepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(IsUiOrCliPath)
            .Select(NormalizeRepositoryRelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var projectViolations = hostProjects
            .SelectMany(projectPath => ReadProjectReferenceNames(projectPath)
                .Where(projectName => projectName is "CrossMacro.Infrastructure")
                .Select(projectName => $"{projectPath}: {projectName}"))
            .ToArray();

        AssertNoViolations(
            projectViolations,
            "UI and CLI library projects must not reference Infrastructure; executable roots own composition.");

        var sourceReferences = Directory.EnumerateFiles(Path.Combine(GetRepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsUiOrCliPath)
            .SelectMany(ReadInfrastructureSourceReferences)
            .Order(StringComparer.Ordinal)
            .ToArray();

        AssertNoViolations(
            sourceReferences,
            "UI and CLI library source must not reference Infrastructure; executable roots own composition.");
    }

    [Fact]
    public void UiAndCliSource_ShouldNotReferenceConcreteClipboardKeyOrInputTypes()
    {
        var forbiddenPatterns = new[]
        {
            "Infrastructure.Services.InputSimulatorPool",
            "Infrastructure.Services.KeyCodeMapper",
            "Infrastructure.Services.InputSimulator",
        };

        var violations = FindTextViolations("src", forbiddenPatterns)
            .Where(violation =>
            {
                var path = violation.Split(':', 2)[0].Replace('\\', '/');
                return path.Split('/').Contains("CrossMacro.UI", StringComparer.Ordinal)
                    || path.Split('/').Contains("CrossMacro.Cli", StringComparer.Ordinal);
            })
            .ToArray();

        AssertNoViolations(
            violations,
            "Production UI and CLI source must depend on clipboard, key, and input contracts rather than concrete Infrastructure implementations.");
    }

    [Fact]
    public void UiSource_ShouldNotReferenceLegacyTextExpansionStorageTypes()
    {
        var forbiddenPatterns = new[]
        {
            "ITextExpansionStorageService",
            "TextExpansionStorageService",
        };
        var uiDirectories = new[]
        {
            "src/CrossMacro.UI",
            "src/CrossMacro.UI.Linux",
            "src/CrossMacro.UI.Windows",
            "src/CrossMacro.UI.MacOS",
        };
        var violations = uiDirectories
            .SelectMany(directory => FindTextViolations(directory, forbiddenPatterns))
            .ToArray();

        AssertNoViolations(
            violations,
            "UI source and design code must use the Application ITextExpansionStore port rather than legacy Infrastructure text-expansion storage types.");
    }

    [Fact]
    public void PlatformInfrastructureReferences_ShouldRemainExplicitPhaseThreeDebt()
    {
        var projectViolations = Directory.EnumerateFiles(Path.Combine(GetRepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).StartsWith("CrossMacro.Platform.", StringComparison.Ordinal))
            .Select(NormalizeRepositoryRelativePath)
            .SelectMany(projectPath => ReadProjectReferenceNames(projectPath)
                .Where(projectName => projectName is "CrossMacro.Infrastructure")
                .Select(projectName => $"{projectPath}: {projectName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var expectedProjectReferences = TemporaryPlatformInfrastructureProjectReferences
            .SelectMany(reference => reference.Value.Select(projectName => $"{reference.Key}: {projectName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedProjectReferences, projectViolations);

        var sourceReferences = Directory.EnumerateFiles(Path.Combine(GetRepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => NormalizeRepositoryRelativePath(path).Split('/').Any(segment => segment is "CrossMacro.Platform.Linux" or "CrossMacro.Platform.MacOS" or "CrossMacro.Platform.Windows")
                && File.ReadLines(path).Any(line => line.Contains("CrossMacro.Infrastructure", StringComparison.Ordinal)))
            .Select(NormalizeRepositoryRelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            TemporaryPlatformInfrastructureSourceFiles.Order(StringComparer.Ordinal),
            sourceReferences,
            StringComparer.Ordinal);
    }

    [Fact]
    public void McpProject_ShouldDeclareItsDirectContractsAndRemainPlatformAgnostic()
    {
        var projectReferences = ReadProjectReferenceNames("src/CrossMacro.Mcp/CrossMacro.Mcp.csproj");

        Assert.Equal(["CrossMacro.Application", "CrossMacro.Cli", "CrossMacro.Core", "CrossMacro.Daemon.Contracts", "CrossMacro.Platform.Abstractions"], projectReferences);

        var violations = FindTextViolations(
            "src/CrossMacro.Mcp",
            [
                "Avalonia",
                "CrossMacro.Infrastructure",
                "CrossMacro.Platform.Linux",
                "CrossMacro.Platform.Windows",
                "CrossMacro.Platform.MacOS",
            ]);

        AssertNoViolations(
            violations,
            "MCP consumes Application and platform contracts plus the CLI bridge; it must not compose concrete runtime adapters.");
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
    public void PlatformServiceRegistrar_ShouldRemainAnAbstractionsOwnedPlatformBoundary()
    {
        var registrarType = typeof(IPlatformServiceRegistrar);

        Assert.Equal("CrossMacro.Platform.Abstractions", registrarType.Assembly.GetName().Name);
        var registrationMethod = Assert.Single(registrarType.GetMethods());
        Assert.Equal("RegisterPlatformServices", registrationMethod.Name);
        Assert.Equal(typeof(void), registrationMethod.ReturnType);
        var parameter = Assert.Single(registrationMethod.GetParameters());
        Assert.Equal(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection), parameter.ParameterType);

    }

    [Fact]
    public void SharedProductionLayers_ShouldNotReferenceConcretePlatformServiceRegistrars()
    {
        var sharedProductionLayers = new[]
        {
            "src/CrossMacro.Application",
            "src/CrossMacro.Cli",
            "src/CrossMacro.Core",
            "src/CrossMacro.Daemon.Contracts",
            "src/CrossMacro.Daemon",
            "src/CrossMacro.Infrastructure",
            "src/CrossMacro.Packaging.Abstractions",
            "src/CrossMacro.Platform.Abstractions",
            "src/CrossMacro.UI",
        };
        var concreteRegistrars = new[]
        {
            "LinuxPlatformServiceRegistrar",
            "WindowsPlatformServiceRegistrar",
            "MacOSPlatformServiceRegistrar",
        };

        var violations = sharedProductionLayers
            .SelectMany(directory => FindTextViolations(directory, concreteRegistrars))
            .Order(StringComparer.Ordinal)
            .ToArray();

        AssertNoViolations(
            violations,
            "Shared production layers must depend on IPlatformServiceRegistrar, not concrete platform registrars. Matching platform libraries and UI platform Program.cs composition roots own concrete registrar references.");
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
            "CrossMacro.Platform.Abstractions",
        };

        var projectReferences = ReadProjectReferenceNames("tests/CrossMacro.Core.Tests/CrossMacro.Core.Tests.csproj");

        var violations = projectReferences
            .Where(reference => !allowedReferences.Contains(reference))
            .Order(StringComparer.Ordinal)
            .ToArray();

        AssertNoViolations(
            violations,
            "Core.Tests may reference only Core and the explicitly retained Platform.Abstractions contract/strategy tests. Infrastructure-owned characterization and serialization tests belong in Infrastructure.Tests.");
    }

    [Fact]
    public void ProductionProjectGraph_ShouldBeAcyclic()
    {
        var projects = Directory.EnumerateFiles(
                Path.Combine(GetRepositoryRoot(), "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .ToDictionary(
                NormalizeRepositoryRelativePath,
                ReadProjectReferenceTargets,
                StringComparer.Ordinal);
        var missingReferences = projects
            .SelectMany(project => project.Value
                .Where(reference => !projects.ContainsKey(reference))
                .Select(reference => $"{project.Key} -> {reference}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        AssertNoViolations(
            missingReferences,
            "Production project references must resolve to a project in the production graph.");
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var cycles = new List<string>();

        foreach (var project in projects.Keys.Order(StringComparer.Ordinal))
        {
            Visit(project, new List<string>());
        }

        AssertNoViolations(
            cycles,
            "Production project references must remain acyclic; composition belongs at executable boundaries.");

        void Visit(string project, List<string> path)
        {
            if (visited.Contains(project))
            {
                return;
            }

            if (!visiting.Add(project))
            {
                var cycleStart = path.IndexOf(project);
                var cycle = cycleStart >= 0
                    ? path.Skip(cycleStart).Append(project)
                    : path.Append(project);
                cycles.Add(string.Join(" -> ", cycle));
                return;
            }

            path.Add(project);
            if (projects.TryGetValue(project, out var references))
            {
                foreach (var reference in references)
                {
                    Visit(reference, path);
                }
            }

            path.RemoveAt(path.Count - 1);
            _ = visiting.Remove(project);
            _ = visited.Add(project);
        }
    }

    [Fact]
    public void ProductionProjectGraph_ShouldNotIntroduceGenericLayerProjects()
    {
        var forbiddenProjectNames = new[]
        {
            "CrossMacro.Common",
            "CrossMacro.Helpers",
            "CrossMacro.Manager",
            "CrossMacro.Shared",
        };
        var violations = Directory.EnumerateFiles(
                Path.Combine(GetRepositoryRoot(), "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => forbiddenProjectNames.Contains(name, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        AssertNoViolations(
            violations,
            "New project layers must have feature ownership; generic Common/Helpers/Manager/Shared projects are prohibited.");
    }

    [Fact]
    public void LinuxNativeProject_ShouldRemainAnExplicitProductionLayer()
    {
        var nativeProjectPath = "src/CrossMacro.Platform.Linux.Native/CrossMacro.Platform.Linux.Native.csproj";
        var linuxProjectPath = "src/CrossMacro.Platform.Linux/CrossMacro.Platform.Linux.csproj";

        Assert.Equal(["CrossMacro.Core"], ReadProjectReferenceNames(nativeProjectPath));
        Assert.Contains(
            "CrossMacro.Platform.Linux.Native",
            ReadProjectReferenceNames(linuxProjectPath),
            StringComparer.Ordinal);
        Assert.Contains(
            "CrossMacro.Platform.Linux.Native",
            ReadProjectReferenceNames("src/CrossMacro.Daemon/CrossMacro.Daemon.csproj"),
            StringComparer.Ordinal);
    }

    private static void AssertNoViolations(IReadOnlyCollection<string> violations, string message)
    {
        Assert.True(
            violations.Count is 0,
            message + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static string[] ReadProjectReferenceNames(string projectPath)
    {
        return ReadProjectDependencies(projectPath)
            .Where(dependency => dependency.Kind is "ProjectReference")
            .Select(dependency => Path.GetFileNameWithoutExtension(dependency.Name.Replace('\\', Path.DirectorySeparatorChar)))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadProjectReferenceTargets(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(Path.Combine(GetRepositoryRoot(), projectPath))!;
        return ReadProjectDependencies(projectPath)
            .Where(dependency => dependency.Kind is "ProjectReference")
            .Select(dependency => NormalizeRepositoryRelativePath(Path.GetFullPath(Path.Combine(
                projectDirectory,
                dependency.Name.Replace('\\', Path.DirectorySeparatorChar)))))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadNonProjectDependencies(string projectPath)
    {
        return ReadProjectDependencies(projectPath)
            .Where(dependency => dependency.Kind is "PackageReference" or "FrameworkReference")
            .Select(dependency => $"{dependency.Kind}: {dependency.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetDependencyName((string Kind, string Name) dependency)
    {
        return dependency.Kind is "ProjectReference"
            ? Path.GetFileNameWithoutExtension(dependency.Name.Replace('\\', Path.DirectorySeparatorChar))
            : dependency.Name;
    }

    private static (string Kind, string Name)[] ReadProjectDependencies(string projectPath)
    {
        var document = XDocument.Load(Path.Combine(GetRepositoryRoot(), projectPath));

        return document
            .Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference" or "FrameworkReference")
            .Select(element => (Kind: element.Name.LocalName, Name: element.Attribute("Include")?.Value))
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency.Name))
            .Select(dependency => (dependency.Kind, Name: dependency.Name!))
            .OrderBy(dependency => dependency.Kind, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] FindProjectTextViolations(string projectPath, IReadOnlyCollection<string> forbiddenPatterns)
    {
        var fullPath = Path.Combine(GetRepositoryRoot(), projectPath);
        return File.ReadLines(fullPath)
            .Select((text, index) => (Number: index + 1, Text: text))
            .SelectMany(line => forbiddenPatterns
                .Where(pattern => line.Text.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => string.Create(CultureInfo.InvariantCulture, $"{NormalizeRepositoryRelativePath(fullPath)}:{line.Number}: contains '{pattern}'")))
            .ToArray();
    }

    private static string[] FindTextViolations(string relativeDirectory, IReadOnlyCollection<string> forbiddenPatterns)
    {
        var root = GetRepositoryRoot();
        var directory = Path.Combine(root, relativeDirectory);

        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(PathSeparators)
                .Any(segment => segment is "obj" or "bin"))
            .Order(StringComparer.Ordinal)
            .SelectMany(path => FindTextViolationsInFile(path, forbiddenPatterns))
            .ToArray();
    }

    private static IEnumerable<string> FindTextViolationsInFile(string path, IReadOnlyCollection<string> forbiddenPatterns)
    {
        var source = File.ReadAllText(path);
        var relativePath = NormalizeRepositoryRelativePath(path);
        return SourceArchitectureInspector.FindReferences(source, forbiddenPatterns)
            .Select(hit => $"{relativePath}:{hit.Line.ToString(CultureInfo.InvariantCulture)}: references '{hit.Pattern}'");
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

    private static bool IsUiOrCliPath(string path)
    {
        var relativePath = NormalizeRepositoryRelativePath(path);
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Contains("CrossMacro.UI", StringComparer.Ordinal)
            || segments.Contains("CrossMacro.Cli", StringComparer.Ordinal);
    }

    private static string NormalizeRepositoryRelativePath(string path)
    {
        return Path.GetRelativePath(GetRepositoryRoot(), path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static IEnumerable<string> ReadInfrastructureSourceReferences(string path)
    {
        var relativePath = NormalizeRepositoryRelativePath(path);
        foreach (var line in File.ReadLines(path))
        {
            foreach (Match match in InfrastructureReferenceRegex.Matches(line))
            {
                var reference = match.Groups[1].Value;
                yield return $"{relativePath}: {reference}";
            }
        }
    }
}
