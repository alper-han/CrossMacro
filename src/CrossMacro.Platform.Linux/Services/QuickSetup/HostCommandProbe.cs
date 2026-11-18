using CrossMacro.Platform.Linux.Helpers;

namespace CrossMacro.Platform.Linux.Services.QuickSetup;

/// <summary>
/// Resolves executables and checks pkexec through the shell that will run them. Local probes search
/// <paramref name="searchPath"/> when supplied and the process <c>PATH</c> otherwise.
/// </summary>
internal sealed class HostCommandProbe(string? searchPath = null)
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private const string ResolveCommand = "path=$(command -v \"$1\") || exit 1; case \"$path\" in /*) ;; *) exit 1 ;; esac; test -f \"$path\" && test -x \"$path\" && printf '%s' \"$path\"";

    // Inspect the process and file as seen by the shell that will actually invoke pkexec.
    // A setuid bit alone is insufficient inside no-new-privileges/user-namespace wrappers.
    internal const string PkexecUsabilityCommand =
        "path=$1\n" +
        "test -f \"$path\" && test -x \"$path\" && test -u \"$path\" || exit 1\n" +
        "test \"$(stat -Lc '%u' -- \"$path\")\" = 0 || exit 1\n" +
        "# findmnt is optional: do not require util-linux for working pkexec.\n" +
        "if command -v findmnt >/dev/null 2>&1 && command -v readlink >/dev/null 2>&1; then\n" +
        "    if target=$(readlink -f -- \"$path\") && options=$(findmnt -n -o VFS-OPTIONS -T \"$target\"); then\n" +
        "        case \",$options,\" in *,nosuid,*) exit 1 ;; esac\n" +
        "    fi\n" +
        "fi\n" +
        "while read -r key value rest; do\n" +
        "    if test \"$key\" = 'NoNewPrivs:'; then\n" +
        "        test \"$value\" = 0\n" +
        "        exit $?\n" +
        "    fi\n" +
        "done < /proc/self/status\n" +
        "exit 1\n";

    public ValueTask<string?> ResolveAsync(string fileName, CancellationToken cancellationToken = default) =>
        RunProbeAsync("/bin/sh", ["-c", ResolveCommand, "crossmacro-command-probe", fileName], searchPath, cancellationToken);

    public static ValueTask<string?> ResolveOnHostViaFlatpakSpawnAsync(string spawnPath, string fileName, CancellationToken cancellationToken = default) =>
        RunProbeAsync(spawnPath, ["--host", "--watch-bus", "--directory=/", "/bin/sh", "-c", ResolveCommand, "crossmacro-command-probe", fileName], searchPath: null, cancellationToken);

    public async ValueTask<bool> PkexecIsUsableAsync(string path, CancellationToken cancellationToken = default) =>
        await RunProbeAsync("/bin/sh", ["-c", PkexecUsabilityCommand, "crossmacro-pkexec-probe", path], searchPath, cancellationToken).ConfigureAwait(false) is not null;

    public static async ValueTask<bool> PkexecIsUsableOnHostViaFlatpakSpawnAsync(string spawnPath, string path, CancellationToken cancellationToken = default) =>
        await RunProbeAsync(spawnPath, ["--host", "--watch-bus", "--directory=/", "/bin/sh", "-c", PkexecUsabilityCommand, "crossmacro-pkexec-probe", path], searchPath: null, cancellationToken).ConfigureAwait(false) is not null;

    public static ValueTask<string?> ReadUidOnHostViaFlatpakSpawnAsync(string spawnPath, CancellationToken cancellationToken = default) =>
        RunProbeAsync(spawnPath, ["--host", "--watch-bus", "--directory=/", "/bin/sh", "-c", "id -u", "crossmacro-identity-probe"], searchPath: null, cancellationToken);

    private static async ValueTask<string?> RunProbeAsync(string fileName, string[] arguments, string? searchPath, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = ExternalCommand.CreateStartInfo(fileName, arguments);
            if (searchPath is not null)
            {
                startInfo.Environment["PATH"] = searchPath;
            }

            var result = await ExternalCommand.RunAsync(startInfo, ProbeTimeout, cancellationToken).ConfigureAwait(false);
            return result.ExitCode is 0 ? result.StdOut : null;
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or OperationCanceledException))
        {
            return null;
        }
    }
}
