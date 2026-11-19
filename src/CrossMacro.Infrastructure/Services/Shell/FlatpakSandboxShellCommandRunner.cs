namespace CrossMacro.Infrastructure.Services.Shell;

public sealed class FlatpakSandboxShellCommandRunner : IShellCommandRunner
{
    internal const string FlatpakSpawnExecutable = "/usr/bin/flatpak-spawn";
    private const string EnvExecutable = "/usr/bin/env";

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
        startInfo.ArgumentList.Add($"--directory={SandboxTempDirectory}");

        // The sandboxed instance starts from this app's environment even with flatpak-spawn
        // --clear-env, so the environment is reset inside the sandbox instead.
        startInfo.ArgumentList.Add(EnvExecutable);
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add($"HOME={SandboxHome}");
        startInfo.ArgumentList.Add($"TMPDIR={SandboxTempDirectory}");
        startInfo.ArgumentList.Add($"PATH={SandboxPath}");
        startInfo.ArgumentList.Add($"LANG={SandboxLocale}");
        startInfo.ArgumentList.Add("/bin/sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(request.Command);
        return startInfo;
    }
}
