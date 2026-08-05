using System.Globalization;
using System.Reflection;
using System.Collections.ObjectModel;

namespace CrossMacro.Core.Tests.Architecture;

public sealed class ArchitectureBoundaryTests
{
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
            .Where(dependency => dependency.Kind is "PackageReference" or "FrameworkReference"
                || !string.Equals(dependency.Kind, "ProjectReference", StringComparison.Ordinal)
                || !string.Equals(GetDependencyName(dependency), "CrossMacro.Core", StringComparison.Ordinal))
            .Select(dependency => $"{dependency.Kind}: {GetDependencyName(dependency)}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        AssertNoViolations(
            violations,
            "CrossMacro.Application must remain Avalonia-free and depend only on Core contracts until adapters are migrated behind ports.");

        AssertNoViolations(
            FindProjectTextViolations(
                "src/CrossMacro.Application/CrossMacro.Application.csproj",
                [
                    "Avalonia",
                    "CrossMacro.Infrastructure",
                    "CrossMacro.Platform.",
                    "Microsoft.Extensions.DependencyInjection",
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
                "Avalonia",
                "Microsoft.Extensions.DependencyInjection",
                "Environment.",
                "OperatingSystem.",
                "RuntimeInformation",
            ]);

        AssertNoViolations(
            violations,
            "CrossMacro.Application source must expose ports and results, not concrete adapters, host frameworks, or runtime composition.");
    }

    [Fact]
    public void ApplicationProject_ShouldContainOnlyCoreOwnedUseCasesAndContracts()
    {
        var applicationSource = Directory.EnumerateFiles(
                Path.Combine(GetRepositoryRoot(), "src/CrossMacro.Application"),
                "*.cs",
                SearchOption.AllDirectories)
            .Select(NormalizeRepositoryRelativePath)
            .Where(path => !path.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("bin", StringComparer.Ordinal)
                && !path.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("obj", StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "src/CrossMacro.Application/Automation/ICachedTextExpansionStore.cs",
                "src/CrossMacro.Application/Automation/IManageSchedule.cs",
                "src/CrossMacro.Application/Automation/IManageShortcut.cs",
                "src/CrossMacro.Application/Automation/IManageTextExpansion.cs",
                "src/CrossMacro.Application/Automation/IManageTrigger.cs",
                "src/CrossMacro.Application/Automation/IProfileTextExpansionStore.cs",
                "src/CrossMacro.Application/Automation/ITextExpansionStore.cs",
                "src/CrossMacro.Application/Automation/ManageSchedule.cs",
                "src/CrossMacro.Application/Automation/ManageShortcut.cs",
                "src/CrossMacro.Application/Automation/ManageTextExpansion.cs",
                "src/CrossMacro.Application/Automation/ManageTrigger.cs",
                "src/CrossMacro.Application/Automation/TaskCollectionResult.cs",
                "src/CrossMacro.Application/Automation/TaskRequest.cs",
                "src/CrossMacro.Application/GlobalUsings.cs",
                "src/CrossMacro.Application/Profiles/IManageProfile.cs",
                "src/CrossMacro.Application/Profiles/ManageProfile.cs",
                "src/CrossMacro.Application/Profiles/ProfileRequest.cs",
                "src/CrossMacro.Application/Profiles/ProfileResult.cs",
                "src/CrossMacro.Application/Runtime/IRunExecutionService.cs",
                "src/CrossMacro.Application/Runtime/IRuntimeLifecycle.cs",
                "src/CrossMacro.Application/Runtime/MacroPlayableActionCounter.cs",
                "src/CrossMacro.Application/Runtime/RunExecutionRequest.cs",
                "src/CrossMacro.Application/Runtime/RunExecutionResult.cs",
                "src/CrossMacro.Application/Runtime/RunExecutionStatus.cs",
                "src/CrossMacro.Application/Runtime/RunScriptInputStep.cs",
                "src/CrossMacro.Application/Runtime/RuntimeLifecycle.cs",
                "src/CrossMacro.Application/Runtime/RuntimeLifecycleStep.cs",
            ],
            applicationSource);
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
    public void UiRuntimeContextConsumers_ShouldUseInjectedPlatformContract()
    {
        var affectedFiles = new[]
        {
            "src/CrossMacro.UI/Services/ExternalUrlOpener.cs",
            "src/CrossMacro.UI/Services/TrayIconService.cs",
            "src/CrossMacro.UI/ViewModels/SettingsViewModel.cs",
        };

        foreach (var relativePath in affectedFiles)
        {
            var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), relativePath));
            Assert.DoesNotContain("CrossMacro.Infrastructure.Services.RuntimeContext", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new RuntimeContext", source, StringComparison.Ordinal);
            Assert.Contains("IRuntimeContext", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ExecutablePlatformRoots_ShouldRegisterOneRuntimeContextContract()
    {
        var roots = new[]
        {
            "src/CrossMacro.UI.Linux/Program.cs",
            "src/CrossMacro.UI.Windows/Program.cs",
            "src/CrossMacro.UI.MacOS/Program.cs",
        };

        foreach (var relativePath in roots)
        {
            var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), relativePath));
            _ = Assert.Single(Regex.Matches(source, "AddSingleton<IRuntimeContext(?:,|>\\()", RegexOptions.NonBacktracking));
            Assert.Contains("RuntimeContext", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CliScreenshotService_ShouldConsumeHostNeutralScreenshotPort()
    {
        var servicePath = Path.Combine(GetRepositoryRoot(), "src/CrossMacro.Cli/Services/ScreenshotCliService.cs");
        var source = File.ReadAllText(servicePath);

        Assert.DoesNotContain("CrossMacro.Infrastructure.Services.ScreenCapture", source, StringComparison.Ordinal);
        Assert.Contains("IScreenshotCaptureService", source, StringComparison.Ordinal);
        Assert.True(
            File.Exists(Path.Combine(GetRepositoryRoot(), "src/CrossMacro.Platform.Abstractions/IScreenshotCaptureService.cs")),
            "The screenshot port must be owned by Platform.Abstractions.");
    }

    [Fact]
    public void EditorViewModel_ShouldConsumeNeutralCoordinateCapturePort()
    {
        var editorPath = Path.Combine(GetRepositoryRoot(), "src/CrossMacro.UI/ViewModels/EditorViewModel.cs");
        var capturePath = Path.Combine(GetRepositoryRoot(), "src/CrossMacro.UI/ViewModels/EditorViewModel.CaptureAndFileOps.cs");
        var editorSource = File.ReadAllText(editorPath);
        var captureSource = File.ReadAllText(capturePath);
        var uiGlobalUsingsPath = Path.Combine(GetRepositoryRoot(), "src/CrossMacro.UI/GlobalUsings.cs");
        var uiGlobalUsings = File.ReadAllText(uiGlobalUsingsPath);

        Assert.True(
            editorSource.Contains("CrossMacro.Platform.Abstractions", StringComparison.Ordinal)
                || uiGlobalUsings.Contains("global using CrossMacro.Platform.Abstractions", StringComparison.Ordinal),
            "EditorViewModel must consume CrossMacro.Platform.Abstractions directly or through the UI project's global usings.");
        Assert.Contains("ICoordinateCaptureService", editorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CrossMacro.Infrastructure.Services.CoordinateCaptureService", editorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CrossMacro.Infrastructure.Services.CoordinateCaptureService", captureSource, StringComparison.Ordinal);
        Assert.True(
            File.Exists(Path.Combine(GetRepositoryRoot(), "src/CrossMacro.Platform.Abstractions/ICoordinateCaptureService.cs")),
            "The coordinate capture port must be owned by Platform.Abstractions.");
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

        Assert.Equal(TemporaryPlatformInfrastructureSourceFiles.Order(StringComparer.Ordinal), sourceReferences);
    }

    [Fact]
    public void MigrationLedger_ShouldNotBeAddedAsBuildArtifact()
    {
        var ledgerPath = Path.Combine(GetRepositoryRoot(), "docs/architecture/migration-ledger.md");
        Assert.False(File.Exists(ledgerPath), "Environment centralization must not add Markdown artifacts.");
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

        var registrarFiles = new[]
        {
            ("LinuxPlatformServiceRegistrar", "src/CrossMacro.Platform.Linux/DependencyInjection/LinuxPlatformServiceRegistrar.cs"),
            ("WindowsPlatformServiceRegistrar", "src/CrossMacro.Platform.Windows/DependencyInjection/WindowsPlatformServiceRegistrar.cs"),
            ("MacOSPlatformServiceRegistrar", "src/CrossMacro.Platform.MacOS/DependencyInjection/MacOSPlatformServiceRegistrar.cs"),
        };

        foreach (var (registrarName, relativePath) in registrarFiles)
        {
            var fullPath = Path.Combine(GetRepositoryRoot(), relativePath);
            Assert.True(File.Exists(fullPath), $"{registrarName} must remain in {relativePath}.");
            Assert.Contains($"class {registrarName} : IPlatformServiceRegistrar", File.ReadAllText(fullPath), StringComparison.Ordinal);
        }
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
    public void CompatibilityBoundaries_ShouldRemainPresentUntilPolicyChanges()
    {
        var requiredPaths = new[]
        {
            "src/CrossMacro.Infrastructure/Services/MacroFileManager.cs",
            "src/CrossMacro.Core/Models/MacroPositionSemantics.cs",
            "src/CrossMacro.Platform.Linux/Services/Factories/LinuxCaptureFactory.cs",
            "src/CrossMacro.Platform.Linux/Services/Factories/LinuxSimulatorFactory.cs",
            "src/CrossMacro.Platform.Linux/Ipc/IpcHandshakeCodec.cs",
        };
        var missing = requiredPaths
            .Where(path => !File.Exists(Path.Combine(GetRepositoryRoot(), path)))
            .ToArray();

        AssertNoViolations(
            missing,
            "Legacy macro readers, Linux native fallbacks, and the daemon handshake codec require an explicit compatibility-policy change before removal.");
    }

    [Fact]
    public void LinuxNativeSources_ShouldBeOwnedByNativeProjectWithoutLinkedCompile()
    {
        var nativeProjectPath = "src/CrossMacro.Platform.Linux.Native/CrossMacro.Platform.Linux.Native.csproj";
        var linuxProjectPath = "src/CrossMacro.Platform.Linux/CrossMacro.Platform.Linux.csproj";
        var nativeProject = File.ReadAllText(Path.Combine(GetRepositoryRoot(), nativeProjectPath));
        var linuxProject = File.ReadAllText(Path.Combine(GetRepositoryRoot(), linuxProjectPath));

        Assert.DoesNotContain("CrossMacro.Platform.Linux/Native", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Compile Include", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Compile Remove=\"Native/Evdev", linuxProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Compile Remove=\"Native/Systemd", linuxProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Compile Remove=\"Native/UInput", linuxProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Compile Remove=\"Native/LinuxSystemPaths.cs", linuxProject, StringComparison.Ordinal);

        var nativeFiles = new[]
        {
            "src/CrossMacro.Platform.Linux.Native/Native/Evdev/EvdevErrorEventArgs.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/Evdev/EvdevInputEventArgs.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/Evdev/EvdevNative.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/Evdev/EvdevReader.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/Evdev/ILinuxInputDeviceAccessProbe.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/Evdev/InputDeviceHelper.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/Evdev/LinuxInputDeviceAccessProbe.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/LinuxSystemPaths.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/Systemd/SystemdNotify.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/UInput/IUInputDevice.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/UInput/UInputDevice.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/UInput/UInputNative.cs",
            "src/CrossMacro.Platform.Linux.Native/Native/UInput/VirtualDeviceConstants.cs",
        };

        var missingNativeFiles = nativeFiles
            .Where(path => !File.Exists(Path.Combine(GetRepositoryRoot(), path)))
            .ToArray();
        AssertNoViolations(
            missingNativeFiles,
            "Evdev, UInput, Systemd and Linux path sources must be physically owned by CrossMacro.Platform.Linux.Native.");

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

    [Fact]
    public void SourceTree_ShouldNotContainEmptySourceFiles()
    {
        var emptyFiles = EnumerateSourceFiles()
            .Where(path => new FileInfo(path).Length <= 3)
            .Select(NormalizeRepositoryRelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        AssertNoViolations(
            emptyFiles,
            "Empty .cs files are refactoring leftovers; delete the file instead of leaving an empty shell.");
    }

    [Fact]
    public void SourceTree_ShouldNotDuplicateSourceFileNamesAcrossProjects()
    {
        var allowedDuplicateNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "AssemblyInfo.cs",
            "GlobalUsings.cs",
            "Program.cs",
            "ServiceCollectionExtensions.cs",
        };

        var duplicates = EnumerateSourceFiles()
            .GroupBy(Path.GetFileName, StringComparer.Ordinal)
            .Where(group => group.Skip(1).Any() && !allowedDuplicateNames.Contains(group.Key!))
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(NormalizeRepositoryRelativePath).Order(StringComparer.Ordinal))}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        AssertNoViolations(
            duplicates,
            "A type's implementation must live in exactly one project; duplicated file names across src projects indicate copy-paste drift. Extend the allow-list only for per-project conventions.");
    }

    private static IEnumerable<string> EnumerateSourceFiles()
    {
        return Directory.EnumerateFiles(Path.Combine(GetRepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var segments = path.Replace('\\', '/').Split('/');
                return !segments.Contains("obj", StringComparer.Ordinal) && !segments.Contains("bin", StringComparer.Ordinal);
            });
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
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin"))
            .Order(StringComparer.Ordinal)
            .SelectMany(path => FindTextViolationsInFile(path, forbiddenPatterns))
            .ToArray();
    }

    private static IEnumerable<string> FindTextViolationsInFile(string path, IReadOnlyCollection<string> forbiddenPatterns)
    {
        var lines = File.ReadLines(path).Select((text, index) => (Number: index + 1, Text: text));
        var relativePath = NormalizeRepositoryRelativePath(path);

        foreach (var line in lines)
        {
            foreach (var pattern in forbiddenPatterns)
            {
                if (line.Text.Contains(pattern, StringComparison.Ordinal))
                {
                    yield return string.Create(CultureInfo.InvariantCulture, $"{relativePath}:{line.Number}: contains '{pattern}'");
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
            foreach (Match match in Regex.Matches(
                line,
                @"(?<![A-Za-z0-9_])(?:global::)?(CrossMacro\.Infrastructure(?:\.[A-Za-z_][A-Za-z0-9_]*)*)",
                RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
                TimeSpan.FromMilliseconds(100)))
            {
                var reference = match.Groups[1].Value;
                yield return $"{relativePath}: {reference}";
            }
        }
    }
}
