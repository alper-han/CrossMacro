
namespace CrossMacro.Infrastructure.Services;

public sealed class ShellCommandRunner : IShellCommandRunner
{
    public Task<ShellCommandResult> RunAsync(
        ShellCommandRequest request,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        return ShellCommandProcessExecutor.RunAsync(request, timeout, CreateStartInfo, cancellationToken);
    }

    private static ProcessStartInfo CreateStartInfo(ShellCommandRequest request)
    {
        var startInfo = ShellCommandProcessExecutor.CreateStartInfo(
            OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            request);

        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/S");
            startInfo.ArgumentList.Add("/C");
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
        }

        startInfo.ArgumentList.Add(request.Command);
        return startInfo;
    }
}
