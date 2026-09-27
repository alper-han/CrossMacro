namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal static class HostCommandProbe
{
    private const string ResolveCommand = "path=$(command -v \"$1\") || exit 1; case \"$path\" in /*) ;; *) exit 1 ;; esac; test -f \"$path\" && test -x \"$path\" && printf '%s' \"$path\"";

    // Inspect the process and file as seen by the shell that will actually invoke pkexec.
    // A setuid bit alone is insufficient inside no-new-privileges/user-namespace wrappers.
    private const string PkexecUsabilityCommand =
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

    public static ValueTask<string?> ResolveAsync(string fileName, CancellationToken cancellationToken = default) =>
        RunProbeAsync("/bin/sh", ["-c", ResolveCommand, "crossmacro-command-probe", fileName], cancellationToken);

    public static ValueTask<string?> ResolveOnHostViaFlatpakSpawnAsync(string spawnPath, string fileName, CancellationToken cancellationToken = default) =>
        RunProbeAsync(spawnPath, ["--host", "--watch-bus", "--directory=/", "/bin/sh", "-c", ResolveCommand, "crossmacro-command-probe", fileName], cancellationToken);

    public static async ValueTask<bool> PkexecIsUsableAsync(string path, CancellationToken cancellationToken = default) =>
        await RunProbeAsync("/bin/sh", ["-c", PkexecUsabilityCommand, "crossmacro-pkexec-probe", path], cancellationToken).ConfigureAwait(false) is not null;

    public static async ValueTask<bool> PkexecIsUsableOnHostViaFlatpakSpawnAsync(string spawnPath, string path, CancellationToken cancellationToken = default) =>
        await RunProbeAsync(spawnPath, ["--host", "--watch-bus", "--directory=/", "/bin/sh", "-c", PkexecUsabilityCommand, "crossmacro-pkexec-probe", path], cancellationToken).ConfigureAwait(false) is not null;

    public static ValueTask<string?> ReadUidOnHostViaFlatpakSpawnAsync(string spawnPath, CancellationToken cancellationToken = default) =>
        RunProbeAsync(spawnPath, ["--host", "--watch-bus", "--directory=/", "/bin/sh", "-c", "id -u", "crossmacro-identity-probe"], cancellationToken);

    private static async ValueTask<string?> RunProbeAsync(string fileName, string[] arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCancellation.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            _ = process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(probeCancellation.Token);
            var errorTask = process.StandardError.ReadToEndAsync(probeCancellation.Token);
            try
            {
                await process.WaitForExitAsync(probeCancellation.Token).ConfigureAwait(false);
                _ = await errorTask.ConfigureAwait(false);
                var output = await outputTask.ConfigureAwait(false);
                return process.ExitCode is 0 ? output : null;
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }
}
