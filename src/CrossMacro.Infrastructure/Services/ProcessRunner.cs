
namespace CrossMacro.Infrastructure.Services;

public class ProcessRunner : IProcessRunner
{
    public async Task<bool> CheckCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileName = System.OperatingSystem.IsWindows() ? "where" : "which";

            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (proc is null)
            {
                return false;
            }

            var outputTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = proc.StandardError.ReadToEndAsync(cancellationToken);
            await WaitForExitOrKillAsync(proc, cancellationToken).ConfigureAwait(false);
            _ = await outputTask.ConfigureAwait(false);
            _ = await errorTask.ConfigureAwait(false);
            return proc.ExitCode is 0;
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

    public async Task RunCommandAsync(string command, string args, string input, CancellationToken cancellationToken = default)
    {
        using var proc = CreateProcess(command, redirectStandardInput: true);
        proc.StartInfo.Arguments = args;
        await RunCommandProcessAsync(proc, input, cancellationToken).ConfigureAwait(false);
    }

    public async Task RunCommandAsync(string command, string[] args, string input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        using var proc = CreateProcess(command, redirectStandardInput: true);
        foreach (var arg in args)
        {
            proc.StartInfo.ArgumentList.Add(arg);
        }

        await RunCommandProcessAsync(proc, input, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteClipboardInputAndCloseAsync(string command, string args, string input, CancellationToken cancellationToken = default)
    {
        var proc = CreateProcess(command, redirectStandardInput: true);
        proc.StartInfo.Arguments = args;
        await WriteClipboardInputAndCloseProcessAsync(proc, input, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteClipboardInputAndCloseAsync(string command, string[] args, string input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var proc = CreateProcess(command, redirectStandardInput: true);
        foreach (var arg in args)
        {
            proc.StartInfo.ArgumentList.Add(arg);
        }

        await WriteClipboardInputAndCloseProcessAsync(proc, input, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteClipboardInputAndCloseAsync(string command, string[] args, ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var proc = CreateProcess(command, redirectStandardInput: true);
        foreach (var arg in args)
        {
            proc.StartInfo.ArgumentList.Add(arg);
        }

        await WriteClipboardInputAndCloseProcessAsync(proc, input, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunCommandProcessAsync(Process proc, string input, CancellationToken cancellationToken)
    {
        _ = proc.Start();
        var errorTask = proc.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await proc.StandardInput.WriteAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
            await proc.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            TryKillProcess(proc);
            throw;
        }

        proc.StandardInput.Close();

        await WaitForExitOrKillAsync(proc, cancellationToken).ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        EnsureSuccessfulExit(proc, error);
    }

    private static async Task WriteClipboardInputAndCloseProcessAsync(Process proc, string input, CancellationToken cancellationToken)
    {
        await WriteClipboardInputAndCloseProcessAsync(proc, Encoding.UTF8.GetBytes(input), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteClipboardInputAndCloseProcessAsync(Process proc, ReadOnlyMemory<byte> input, CancellationToken cancellationToken)
    {
        using var streamReadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<string>? errorTask = null;
        Task? stderrTimeoutTask = null;
        try
        {
            _ = proc.Start();
            errorTask = proc.StandardError.ReadToEndAsync(streamReadCts.Token);
            await proc.StandardInput.BaseStream.WriteAsync(input, cancellationToken).ConfigureAwait(false);
            await proc.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            proc.StandardInput.Close();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var exited = false;
            try
            {
                await proc.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                exited = true;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Clipboard owner staying alive; stop the stderr read before releasing its process.
                await CancelAndAwaitTasksAsync(streamReadCts, errorTask, stderrTimeoutTask).ConfigureAwait(false);
                return;
            }

            if (exited)
            {
                // Wrapper child may hold stderr pipe; don't block on it if we succeeded.
                string error = string.Empty;
                if (proc.ExitCode is not 0)
                {
                    stderrTimeoutTask = Task.Delay(TimeSpan.FromMilliseconds(500), TimeProvider.System, streamReadCts.Token);
                    var completed = await Task.WhenAny(errorTask, stderrTimeoutTask).ConfigureAwait(false);
                    if (completed == errorTask)
                    {
                        error = await errorTask.ConfigureAwait(false);
                        await CancelAndAwaitTasksAsync(streamReadCts, errorTask, stderrTimeoutTask).ConfigureAwait(false);
                    }
                    else
                    {
                        await CancelAndAwaitTasksAsync(streamReadCts, errorTask, stderrTimeoutTask).ConfigureAwait(false);
                    }
                }
                else
                {
                    await CancelAndAwaitTasksAsync(streamReadCts, errorTask, stderrTimeoutTask).ConfigureAwait(false);
                }

                EnsureSuccessfulExit(proc, error);
            }
        }
        catch (OperationCanceledException)
        {
            await CancelAndAwaitTasksAsync(streamReadCts, errorTask, stderrTimeoutTask).ConfigureAwait(false);
            TryKillProcess(proc);
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await CancelAndAwaitTasksAsync(streamReadCts, errorTask, stderrTimeoutTask).ConfigureAwait(false);
            TryKillProcess(proc);
            throw;
        }
        finally
        {
            proc.Dispose();
        }
    }

    private static async Task CancelAndAwaitTasksAsync(
        CancellationTokenSource streamReadCts,
        Task? readTask,
        Task? timeoutTask)
    {
        await streamReadCts.CancelAsync().ConfigureAwait(false);
        await ObserveTaskAsync(readTask).ConfigureAwait(false);
        await ObserveTaskAsync(timeoutTask).ConfigureAwait(false);
    }

    private static async Task ObserveTaskAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Preserve the operation's original exception while settling owned tasks.
        }
    }

    public async Task ExecuteCommandAsync(string command, string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        using var proc = CreateProcess(command, redirectStandardInput: false, redirectStandardOutput: true);

        foreach (var arg in args)
        {
            proc.StartInfo.ArgumentList.Add(arg);
        }

        _ = proc.Start();
        var outputTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = proc.StandardError.ReadToEndAsync(cancellationToken);
        await WaitForExitOrKillAsync(proc, cancellationToken).ConfigureAwait(false);
        _ = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        EnsureSuccessfulExit(proc, error);
    }

    public async Task<string> ReadCommandAsync(string command, string args, CancellationToken cancellationToken = default)
    {
        using var proc = CreateProcess(command, redirectStandardInput: false, redirectStandardOutput: true);
        proc.StartInfo.Arguments = args;
        return await ReadCommandProcessAsync(proc, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ReadCommandAsync(string command, string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        using var proc = CreateProcess(command, redirectStandardInput: false, redirectStandardOutput: true);
        foreach (var arg in args)
        {
            proc.StartInfo.ArgumentList.Add(arg);
        }

        return await ReadCommandProcessAsync(proc, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReadCommandProcessAsync(Process proc, CancellationToken cancellationToken)
    {
        _ = proc.Start();
        var resultTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = proc.StandardError.ReadToEndAsync(cancellationToken);
        await WaitForExitOrKillAsync(proc, cancellationToken).ConfigureAwait(false);
        var result = await resultTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        EnsureSuccessfulExit(proc, error);
        return result;
    }

    private static Process CreateProcess(
        string command,
        bool redirectStandardInput,
        bool redirectStandardOutput = false,
        bool redirectStandardError = true)
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardInput = redirectStandardInput,
                RedirectStandardOutput = redirectStandardOutput,
                RedirectStandardError = redirectStandardError,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
    }

    private static async Task WaitForExitOrKillAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Cancellation is best-effort; callers still observe the original failure/cancellation.
        }
    }

    private static void EnsureSuccessfulExit(Process process, string error)
    {
        if (process.ExitCode is 0)
        {
            return;
        }

        var message = string.IsNullOrWhiteSpace(error)
            ? $"Command '{process.StartInfo.FileName}' exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}."
            : $"Command '{process.StartInfo.FileName}' exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}: {error.Trim()}";
        throw new InvalidOperationException(message);
    }
}
