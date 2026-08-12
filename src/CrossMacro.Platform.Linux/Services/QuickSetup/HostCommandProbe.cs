
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal static class HostCommandProbe
{
    private const string PkexecUsabilityCommand = "path=$(command -v pkexec) && test -x \"$path\" && test -u \"$path\"";

    public static async ValueTask<bool> CommandExistsAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Exit-code based: a login shell's profile output would break stdout comparison.
            return await RunCommandSucceedsAsync(
                "sh",
                ["-c", $"command -v {fileName} >/dev/null 2>&1"],
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    public static async ValueTask<bool> CommandExistsOnHostViaFlatpakSpawnAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunCommandSucceedsAsync(
                "flatpak-spawn",
                ["--host", "sh", "-c", $"command -v {fileName} >/dev/null 2>&1"],
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    public static async ValueTask<bool> PkexecIsUsableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunCommandSucceedsAsync(
                "sh",
                ["-c", PkexecUsabilityCommand],
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    public static async ValueTask<bool> PkexecIsUsableOnHostViaFlatpakSpawnAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunCommandSucceedsAsync(
                "flatpak-spawn",
                ["--host", "sh", "-c", PkexecUsabilityCommand],
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    private static async ValueTask<bool> RunCommandSucceedsAsync(string fileName, string[] arguments, CancellationToken cancellationToken)
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

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        _ = await errorTask.ConfigureAwait(false);
        _ = await outputTask.ConfigureAwait(false);
        return process.ExitCode is 0;
    }
}
