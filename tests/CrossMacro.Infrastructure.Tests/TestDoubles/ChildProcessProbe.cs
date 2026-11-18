using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.TestDoubles;

/// <summary>
/// Lets a test observe a child process that a shell command spawned: the command records the
/// child's pid in a file, and the probe reports when the file appears and whether the process is gone.
/// </summary>
internal sealed class ChildProcessProbe : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan ObservationLimit = TimeSpan.FromSeconds(10);

    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("crossmacro-child-probe-");

    public string PidFilePath => Path.Combine(_directory.FullName, "child.pid");

    /// <summary>A shell command that starts a long-lived child, records its pid and waits for it.</summary>
    public string LongLivedChildCommand => $"sleep 60 & echo $! > '{PidFilePath}'; wait";

    public async Task<int> WaitForChildStartedAsync(CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(ObservationLimit);

        while (true)
        {
            if (File.Exists(PidFilePath)
                && int.TryParse(await File.ReadAllTextAsync(PidFilePath, limit.Token), CultureInfo.InvariantCulture, out var pid))
            {
                return pid;
            }

            await Task.Delay(PollInterval, TimeProvider.System, limit.Token);
        }
    }

    public static async Task WaitForChildGoneAsync(int pid, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(ObservationLimit);

        while (IsRunning(pid))
        {
            await Task.Delay(PollInterval, TimeProvider.System, limit.Token);
        }
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            var stat = File.ReadAllText($"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/stat");
            return !stat.Contains(") Z ", StringComparison.Ordinal);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}
