namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class QuickSetupHostCommandLauncherTests
{
    [Fact]
    public async Task DirectPolkitLauncher_WhenPkexecIsDisabledAndRun0IsUnavailable_ReturnsSpecificFailure()
    {
        var launcher = new DirectPolkitHostCommandLauncher(
            (command, _) => ValueTask.FromResult(command is "pkexec"),
            _ => ValueTask.FromResult(false));

        var result = await launcher.IsAvailableAsync();

        Assert.False(result.IsAvailable);
        Assert.Contains("setuid-root", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FlatpakPkexecLauncher_WhenPkexecIsDisabledAndRun0IsUnavailable_ReturnsSpecificFailure()
    {
        var launcher = new FlatpakHostCommandLauncher(
            (_, _) => ValueTask.FromResult(true),
            (command, _) => ValueTask.FromResult(command is "pkexec"),
            _ => ValueTask.FromResult(false));

        var result = await launcher.IsAvailableAsync();

        Assert.False(result.IsAvailable);
        Assert.Contains("setuid-root", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DirectPolkitLauncher_WhenPkexecIsDisabledAndRun0IsAvailable_UsesRun0()
    {
        var launcher = new DirectPolkitHostCommandLauncher(
            (command, _) => ValueTask.FromResult(command is "pkexec" or "run0"),
            _ => ValueTask.FromResult(false));

        var result = await launcher.IsAvailableAsync();
        var startInfo = launcher.CreateStartInfo("true", new LinuxQuickSetupIdentity("1000", "uid:1000"));

        Assert.True(result.IsAvailable);
        Assert.Equal("run0", startInfo.FileName);
        Assert.Equal("--description=CrossMacro temporary input setup", startInfo.ArgumentList[0]);
        Assert.Equal("/bin/sh", startInfo.ArgumentList[1]);
        Assert.Equal("1000", startInfo.ArgumentList[^1]);
    }

    [Fact]
    public async Task FlatpakPkexecLauncher_WhenPkexecIsDisabledAndRun0IsAvailable_UsesHostRun0()
    {
        var launcher = new FlatpakHostCommandLauncher(
            (_, _) => ValueTask.FromResult(true),
            (command, _) => ValueTask.FromResult(command is "pkexec" or "run0"),
            _ => ValueTask.FromResult(false));

        var result = await launcher.IsAvailableAsync();
        var startInfo = launcher.CreateStartInfo("true", new LinuxQuickSetupIdentity("1000", "uid:1000"));

        Assert.True(result.IsAvailable);
        Assert.Equal("flatpak-spawn", startInfo.FileName);
        Assert.Equal("--host", startInfo.ArgumentList[0]);
        Assert.Equal("run0", startInfo.ArgumentList[1]);
        Assert.Equal("--description=CrossMacro temporary input setup", startInfo.ArgumentList[2]);
        Assert.Equal("/bin/sh", startInfo.ArgumentList[3]);
        Assert.Equal("1000", startInfo.ArgumentList[^1]);
    }

    [Fact]
    public async Task FlatpakPkexecLauncher_WhenPkexecIsUsable_PrefersPkexec()
    {
        var launcher = new FlatpakHostCommandLauncher(
            (_, _) => ValueTask.FromResult(true),
            (command, _) => ValueTask.FromResult(command is "pkexec" or "run0"),
            _ => ValueTask.FromResult(true));

        var result = await launcher.IsAvailableAsync();
        var startInfo = launcher.CreateStartInfo("true", new LinuxQuickSetupIdentity("1000", "uid:1000"));

        Assert.True(result.IsAvailable);
        Assert.Equal("pkexec", startInfo.ArgumentList[1]);
        Assert.Equal("/bin/sh", startInfo.ArgumentList[2]);
    }
}
