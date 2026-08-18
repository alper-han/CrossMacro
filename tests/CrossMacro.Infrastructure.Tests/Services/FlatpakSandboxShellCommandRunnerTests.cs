namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class FlatpakSandboxShellCommandRunnerTests
{
    [Fact]
    public void CreateStartInfo_UsesFullySandboxedFlatpakSpawnContract()
    {
        const string command = "flatpak-spawn --host true";
        var runner = new FlatpakSandboxShellCommandRunner();

        var startInfo = runner.CreateStartInfo(new ShellCommandRequest(command, StandardInput: "input"));

        Assert.Equal(FlatpakSandboxShellCommandRunner.FlatpakSpawnExecutable, startInfo.FileName);
        Assert.Equal(
            [
                "--sandbox",
                "--no-network",
                "--watch-bus",
                "--clear-env",
                "--directory=/tmp",
                "--env=HOME=/tmp",
                "--env=TMPDIR=/tmp",
                "--env=PATH=/app/bin:/usr/bin",
                "--env=LANG=C.UTF-8",
                "/bin/sh",
                "-c",
                command,
            ],
            startInfo.ArgumentList);
        Assert.DoesNotContain("--host", startInfo.ArgumentList, StringComparer.Ordinal);
        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
    }

    [Fact]
    public void CreateStartInfo_WithoutStandardInput_DoesNotRedirectInput()
    {
        var runner = new FlatpakSandboxShellCommandRunner();

        var startInfo = runner.CreateStartInfo(new ShellCommandRequest("true"));

        Assert.False(startInfo.RedirectStandardInput);
    }

    [Fact]
    public async Task RunAsync_UsesSharedCaptureInputExitAndOutputLimitContract()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        await using var launcher = await TestLauncher.CreateAsync();

        var result = await launcher.Runner.RunAsync(
            new ShellCommandRequest(
                "IFS= read -r input || :; printf %s \"$input\"; printf failure >&2; exit 7",
                StandardInput: "hello stdin",
                OutputLimitChars: 5),
            timeout: null,
            CancellationToken.None);

        Assert.Equal(7, result.ExitCode);
        Assert.Equal("hello", result.StandardOutput);
        Assert.Equal("failu", result.StandardError);
    }

    [Fact(Timeout = 5000)]
    public async Task RunAsync_WhenTimeoutExpires_UsesSharedTimeoutContract()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        await using var launcher = await TestLauncher.CreateAsync();

        _ = await Assert.ThrowsAsync<ShellCommandTimeoutException>(() =>
            launcher.Runner.RunAsync(
                new ShellCommandRequest("sleep 10"),
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None));
    }

    [Fact(Timeout = 5000)]
    public async Task RunAsync_WhenCallerCancels_UsesSharedCancellationContract()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        await using var launcher = await TestLauncher.CreateAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100), TimeProvider.System);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            launcher.Runner.RunAsync(
                new ShellCommandRequest("sleep 10"),
                timeout: null,
                cancellation.Token));
    }

    private sealed class TestLauncher(string path) : IAsyncDisposable
    {
        private const string Script =
            "#!/bin/sh\n" +
            "while [ \"$#\" -gt 0 ]; do\n" +
            "    case \"$1\" in\n" +
            "        --sandbox|--no-network|--watch-bus|--clear-env|--directory=*) shift ;;\n" +
            "        --env=PATH=*) shift ;;\n" +
            "        --env=*) export \"${1#--env=}\"; shift ;;\n" +
            "        *) break ;;\n" +
            "    esac\n" +
            "done\n" +
            "exec \"$@\"\n";

        private readonly string _path = path;

        internal FlatpakSandboxShellCommandRunner Runner { get; } = new(path);

        internal static async Task<TestLauncher> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"crossmacro-flatpak-spawn-{Guid.NewGuid():N}");
            await File.WriteAllTextAsync(path, Script, CancellationToken.None);
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return new TestLauncher(path);
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            return ValueTask.CompletedTask;
        }
    }
}
