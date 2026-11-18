using CrossMacro.Core;

namespace CrossMacro.Infrastructure.Tests.Services;

[Collection("EnvironmentVariableSensitive")]
public sealed class ApplicationPathsEnvironmentTests
{
    [Fact]
    public void CaptureCurrent_ConfigDirectoryContainsAppIdentifier()
    {
        var result = ApplicationPathsEnvironment.CaptureCurrent().ConfigDirectory;

        Assert.Contains(AppConstants.AppIdentifier, result, StringComparison.Ordinal);
    }

    [Fact]
    public void GetConfigFilePath_CombinesDirectoryAndFileName()
    {
        var paths = ApplicationPathsEnvironment.CaptureCurrent();

        Assert.Equal(Path.Combine(paths.ConfigDirectory, "test.json"), paths.GetConfigFilePath("test.json"));
    }

    [LinuxFact]
    public void CaptureCurrent_UsesXdgConfigHomeWhenItIsAbsolute()
    {
        using var environment = new EnvironmentVariableScope();
        environment.Set("XDG_CONFIG_HOME", environment.Root);

        var result = ApplicationPathsEnvironment.CaptureCurrent().ConfigDirectory;

        Assert.Equal(Path.Combine(environment.Root, AppConstants.AppIdentifier), result);
    }

    [LinuxTheory]
    [InlineData("   ")]
    [InlineData("relative-config")]
    public void CaptureCurrent_WhenXdgConfigHomeIsUnusable_FallsBackToDefaultLinuxPath(string xdgConfigHome)
    {
        using var environment = new EnvironmentVariableScope();
        environment.Set("XDG_CONFIG_HOME", xdgConfigHome);

        var result = ApplicationPathsEnvironment.CaptureCurrent().ConfigDirectory;

        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", AppConstants.AppIdentifier),
            result);
    }
}
