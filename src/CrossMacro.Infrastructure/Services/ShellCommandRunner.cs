
namespace CrossMacro.Infrastructure.Services;

public sealed class ShellCommandRunner : IShellCommandRunner
{
    private static readonly TimeSpan KillWaitTimeout = TimeSpan.FromSeconds(2);

    public async Task<ShellCommandResult> RunAsync(
        ShellCommandRequest request,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            throw new ArgumentException("Shell command cannot be empty.", nameof(request));
        }

        if (request.OutputLimitChars < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Output limit must be >= 0.");
        }

        using var process = CreateShellProcess(request);
        using var timeoutCts = timeout is { } timeoutValue && timeoutValue > TimeSpan.Zero
            ? new CancellationTokenSource(timeoutValue)
            : null;
        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        _ = process.Start();
        var outputTask = ReadBoundedAsync(process.StandardOutput, request.OutputLimitChars, linkedCts.Token);
        var errorTask = ReadBoundedAsync(process.StandardError, request.OutputLimitChars, linkedCts.Token);

        try
        {
            if (request.StandardInput is not null)
            {
                await WriteStandardInputAsync(process.StandardInput, request.StandardInput, linkedCts.Token).ConfigureAwait(false);
            }

            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            return new ShellCommandResult(process.ExitCode, output, error);
        }
        catch (OperationCanceledException ex) when (timeoutCts is { IsCancellationRequested: true } && !cancellationToken.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            await WaitForKilledProcessAsync(process).ConfigureAwait(false);
            throw new ShellCommandTimeoutException(request.Command, timeout!.Value, ex);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            await WaitForKilledProcessAsync(process).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            TryKillProcessTree(process);
            throw;
        }
    }

    private static Process CreateShellProcess(ShellCommandRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            RedirectStandardInput = request.StandardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

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
        return new Process { StartInfo = startInfo };
    }

    private static async Task WriteStandardInputAsync(
        StreamWriter writer,
        string standardInput,
        CancellationToken cancellationToken)
    {
        try
        {
            await writer.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Child processes are allowed to ignore or close stdin; the exit code remains authoritative.
        }
        finally
        {
            CloseStandardInput(writer);
        }
    }

    private static void CloseStandardInput(StreamWriter writer)
    {
        try
        {
            writer.Close();
        }
        catch (IOException)
        {
            // Best-effort stdin finalization; the child process result remains authoritative.
        }
        catch (ObjectDisposedException)
        {
            // The stream may already be closed after a child-side stdin close.
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var builder = new StringBuilder(Math.Min(limit, buffer.Length));
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read is 0)
            {
                return builder.ToString();
            }

            var remaining = limit - builder.Length;
            if (remaining > 0)
            {
                _ = builder.Append(buffer, 0, Math.Min(read, remaining));
            }
        }
    }

    private static async Task WaitForKilledProcessAsync(Process process)
    {
        try
        {
            using var timeout = new CancellationTokenSource(KillWaitTimeout);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Best-effort cleanup after cancellation or timeout; caller observes the original failure.
        }
    }

    private static void TryKillProcessTree(Process process)
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
            // Best-effort cleanup after cancellation or timeout; caller observes the original failure.
        }
    }
}
