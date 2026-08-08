namespace CrossMacro.UI.Tests.DependencyInjection;

/// <summary>
/// Verifies the platform composition roots without loading native Avalonia or
/// OS APIs. Linux gets an additional executable DI test; these source contracts
/// keep Windows and macOS registration drift visible on every CI runner.
/// </summary>
public sealed class HostCompositionContractTests
{
    public static IEnumerable<object[]> HostCases()
    {
        yield return [
            "src/CrossMacro.UI.Linux/Program.cs",
            "LinuxPlatformServiceRegistrar.RegisterPlatformServices",
            "LinuxNativeClipboardService",
            "LinuxEnvironmentSnapshot",
        ];
        yield return [
            "src/CrossMacro.UI.Windows/Program.cs",
            "new WindowsPlatformServiceRegistrar().RegisterPlatformServices",
            "WindowsPlatformServiceRegistrar.RegisterGuiClipboardServices",
            "RuntimeContext",
        ];
        yield return [
            "src/CrossMacro.UI.MacOS/Program.cs",
            "new MacOSPlatformServiceRegistrar().RegisterPlatformServices",
            "NoOpImageClipboardService",
            "RuntimeContext",
        ];
    }

    [Theory]
    [MemberData(nameof(HostCases))]
    public void HostProgram_UsesSharedLifecycleAndPlatformOwnedRegistrations(
        string relativePath,
        string platformRegistration,
        string clipboardRegistration,
        string runtimeContext)
    {
        var source = ReadRepositoryFile(relativePath);

        Assert.Contains("CliGuiRuntime.RunAsync", source, StringComparison.Ordinal);
        Assert.Contains("GuiHostBootstrap.ConfigureGuiRuntimeServices", source, StringComparison.Ordinal);
        Assert.Contains("GuiHostBootstrap.CreateBootstrapCallbacks()", source, StringComparison.Ordinal);
        Assert.Contains("GuiHostBootstrap.AddCommonGuiServices(services)", source, StringComparison.Ordinal);
        Assert.Contains("AddCrossMacroCommonRuntimeServices()", source, StringComparison.Ordinal);
        Assert.Contains("AddCrossMacroSharedPostPlatformRuntimeServices", source, StringComparison.Ordinal);
        Assert.Contains(platformRegistration, source, StringComparison.Ordinal);
        Assert.Contains(clipboardRegistration, source, StringComparison.Ordinal);
        Assert.Contains(runtimeContext, source, StringComparison.Ordinal);
        Assert.Contains("internal static class Program", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HostProjects_KeepNativeBootstrapOwnershipAndCommonAotPolicy()
    {
        var expectedProjects = new[]
        {
            "src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj",
            "src/CrossMacro.UI.Windows/CrossMacro.UI.Windows.csproj",
            "src/CrossMacro.UI.MacOS/CrossMacro.UI.MacOS.csproj",
        };

        foreach (var projectPath in expectedProjects)
        {
            var project = ReadRepositoryFile(projectPath);

            Assert.Contains("<AssemblyName>CrossMacro.UI</AssemblyName>", project, StringComparison.Ordinal);
            Assert.Contains("<Compile Include=\"../Shared/GuiHostBootstrap.cs\"", project, StringComparison.Ordinal);
            Assert.Contains("<PublishAot Condition=\"'$(PublishAot)' == ''\">false</PublishAot>", project, StringComparison.Ordinal);
        }

        var buildPolicy = ReadRepositoryFile("Directory.Build.props");
        Assert.Contains("<IsAotCompatible>true</IsAotCompatible>", buildPolicy, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the CrossMacro repository root.");
    }
}
