namespace CrossMacro.Infrastructure.Services;

public sealed class FlatpakSandboxShellCommandRunner : IShellCommandRunner
{
    internal const string FlatpakSpawnExecutable = "/usr/bin/flatpak-spawn";

#pragma warning disable S5443 // The nested Flatpak sandbox provides an isolated, private /tmp.
    private const string SandboxHome = "/tmp";
    private const string SandboxLocale = "C.UTF-8";
    private const string SandboxPath = "/app/bin:/usr/bin";
    private const string SandboxTempDirectory = "/tmp";
#pragma warning restore S5443

    private readonly string _flatpakSpawnExecutable;

    public FlatpakSandboxShellCommandRunner()
        : this(FlatpakSpawnExecutable)
    {
    }

    internal FlatpakSandboxShellCommandRunner(string flatpakSpawnExecutable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flatpakSpawnExecutable);
        _flatpakSpawnExecutable = flatpakSpawnExecutable;
    }

    public Task<ShellCommandResult> RunAsync(
        ShellCommandRequest request,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        return ShellCommandProcessExecutor.RunAsync(request, timeout, CreateStartInfo, cancellationToken);
    }

    internal ProcessStartInfo CreateStartInfo(ShellCommandRequest request)
    {
        var startInfo = ShellCommandProcessExecutor.CreateStartInfo(_flatpakSpawnExecutable, request);
        startInfo.ArgumentList.Add("--sandbox");
        startInfo.ArgumentList.Add("--no-network");
        startInfo.ArgumentList.Add("--watch-bus");
        startInfo.ArgumentList.Add("--clear-env");
        startInfo.ArgumentList.Add($"--directory={SandboxTempDirectory}");
        startInfo.ArgumentList.Add($"--env=HOME={SandboxHome}");
        startInfo.ArgumentList.Add($"--env=TMPDIR={SandboxTempDirectory}");
        startInfo.ArgumentList.Add($"--env=PATH={SandboxPath}");
        startInfo.ArgumentList.Add($"--env=LANG={SandboxLocale}");
        startInfo.ArgumentList.Add("/bin/sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(request.Command);
        return startInfo;
    }
}
