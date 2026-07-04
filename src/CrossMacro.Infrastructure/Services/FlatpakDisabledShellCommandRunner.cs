using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Infrastructure.Services;

public sealed class FlatpakDisabledShellCommandRunner : IShellCommandRunner
{
    public Task<ShellCommandResult> RunAsync(
        ShellCommandRequest request,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Shell script steps are disabled in Flatpak builds to keep commands inside the sandbox. " +
            "Use a native or AppImage build to run shell steps.");
    }
}
