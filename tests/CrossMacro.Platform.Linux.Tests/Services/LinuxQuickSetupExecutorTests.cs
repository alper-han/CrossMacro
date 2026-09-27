namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxQuickSetupExecutorTests
{
    [Theory]
    [InlineData("/usr/bin/pkexec", 126, "", QuickSetupOutcome.Cancelled)]
    [InlineData("/usr/bin/flatpak-spawn", 126, "", QuickSetupOutcome.Cancelled)]
    [InlineData("/usr/bin/run0", 126, "", QuickSetupOutcome.Failed)]
    [InlineData("/usr/bin/run0", 1, "Failed to start transient service unit: Access denied", QuickSetupOutcome.AuthorizationDenied)]
    [InlineData("/usr/bin/pkexec", 127, "Error executing command as another user: Not authorized", QuickSetupOutcome.AuthorizationDenied)]
    [InlineData("/usr/bin/run0", 1, "Interactive authentication required.", QuickSetupOutcome.AuthorizationDenied)]
    [InlineData("/usr/bin/pkexec", 127, "warning\nError creating textual authentication agent: Error opening current controlling terminal for the process (`/dev/tty'): No such device or address", QuickSetupOutcome.AuthenticationUnavailable)]
    [InlineData("/usr/bin/pkexec", 127, "warning\npkexec must be setuid root", QuickSetupOutcome.PrivilegeUnavailable)]
    [InlineData("/usr/bin/pkexec", 127, "No authentication agent found.", QuickSetupOutcome.AuthenticationUnavailable)]
    [InlineData("/usr/bin/pkexec", 127, "authentication agent dismissed the dialog\nNot authorized", QuickSetupOutcome.AuthorizationDenied)]
    [InlineData("/usr/bin/pkexec", 22, "setfacl is missing on host", QuickSetupOutcome.Failed)]
    public async Task RunAsync_ClassifiesActualAuthorizationBoundary(string command, int exitCode, string stderr, QuickSetupOutcome expected)
    {
        var attempts = 0;
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => 1000, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)),
            (_, _) => { attempts++; return Task.FromResult((exitCode, string.Empty, stderr)); });
        var result = await executor.RunAsync(new FakeLauncher(command), LinuxQuickSetupScriptOptions.Strict, "Test", "unexpected", TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Outcome);
        Assert.False(result.Success);
        Assert.Equal(1, attempts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task RunAsync_WhenDiagnosticsAreLarge_ReturnsBoundedDetails(int exitCode)
    {
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => 1000, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)),
            (_, _) => Task.FromResult((exitCode, new string('o', 10000), new string('e', 10000))));
        var result = await executor.RunAsync(new FakeLauncher(), LinuxQuickSetupScriptOptions.Strict, "Test", "unexpected", TestContext.Current.CancellationToken);
        Assert.Equal(exitCode is 0, result.Success);
        Assert.InRange(result.Message.Length, 1, 800);
    }

    [Fact]
    public async Task RunAsync_WhenLauncherUnavailable_DoesNotAttemptPrivilegeCommand()
    {
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => 1000, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)),
            (_, _) => throw new InvalidOperationException("Privilege command must not be run"));
        var result = await executor.RunAsync(new FakeLauncher(available: false), LinuxQuickSetupScriptOptions.Strict, "Test", "unexpected", TestContext.Current.CancellationToken);
        Assert.Equal(QuickSetupOutcome.PrivilegeUnavailable, result.Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_WhenPreparationFails_DoesNotAuthorizeAndPreservesCancellation(bool cancelled)
    {
        var attempts = 0;
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => 1000, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)),
            (_, _) => { attempts++; return Task.FromResult((0, string.Empty, string.Empty)); });
        Exception failure = cancelled ? new OperationCanceledException() : new IOException("Controlled command probe failure");
        var launcher = new DirectPolkitHostCommandLauncher(
            (_, _) => ValueTask.FromException<string?>(failure),
            (_, _) => ValueTask.FromResult(false));
        var running = executor.RunAsync(launcher, LinuxQuickSetupScriptOptions.Strict, "Test", "unexpected", TestContext.Current.CancellationToken);
        if (cancelled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
        else
        {
            Assert.Equal(QuickSetupOutcome.Failed, (await running).Outcome);
        }
        Assert.Equal(0, attempts);
    }


    [Fact]
    public async Task RunAsync_WhenProcessIsCanceled_StopsUnprivilegedChildBeforePropagatingCancellation()
    {
        var pidPath = Path.GetTempFileName();
        Process? child = null;
        using var cancellation = new CancellationTokenSource();
        var executor = new LinuxQuickSetupExecutor(new LinuxQuickSetupIdentityResolver(() => 1000, () => "0 0 4294967295", _ => ValueTask.FromResult<uint?>(null)));
        var running = executor.RunAsync(new FakeLauncher(script: $"echo $$ > '{pidPath}'; exec sleep 30"), LinuxQuickSetupScriptOptions.Strict, "Test", "unexpected", cancellation.Token);
        try
        {
            var waitForChild = Task.Run(async () =>
            {
                while (true)
                {
                    var pidText = await File.ReadAllTextAsync(pidPath, TestContext.Current.CancellationToken);
                    if (int.TryParse(pidText.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var pid))
                    {
                        return Process.GetProcessById(pid);
                    }
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                }
            }, TestContext.Current.CancellationToken);
            child = await waitForChild.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            Assert.True(child.HasExited);
        }
        finally
        {
            cancellation.Cancel();
            if (child is not null)
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                }
                child.Dispose();
            }
            File.Delete(pidPath);
        }
    }

    private sealed class FakeLauncher(string command = "/usr/bin/pkexec", bool available = true, string? script = null) : IPrivilegedHostCommandLauncher
    {
        public ValueTask<LinuxQuickSetupIdentity?> ResolveIdentityAsync(LinuxQuickSetupIdentityResolver resolver, CancellationToken cancellationToken = default)
            => resolver.ResolveAsync(cancellationToken);

        public ValueTask<(ProcessStartInfo? StartInfo, string FailureMessage)> CreateStartInfoAsync(
            string hostScript, LinuxQuickSetupIdentity identity, CancellationToken cancellationToken = default)
        {
            if (!available)
            {
                return ValueTask.FromResult<(ProcessStartInfo?, string)>((null, "No usable privilege mechanism"));
            }

            var info = new ProcessStartInfo(script is null ? command : "/bin/sh")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (Path.GetFileName(command) is "flatpak-spawn")
            {
                info.ArgumentList.Add("--host");
                info.ArgumentList.Add("/usr/bin/pkexec");
            }
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(script ?? hostScript);
            return ValueTask.FromResult<(ProcessStartInfo?, string)>((info, string.Empty));
        }
    }
}
