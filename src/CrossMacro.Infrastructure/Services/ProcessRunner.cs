using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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
                UseShellExecute = false,
                CreateNoWindow = true
            });
            
            if (proc == null) return false;
            
            await proc.WaitForExitAsync(cancellationToken);
            return proc.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    public async Task RunCommandAsync(string command, string args, string input, CancellationToken cancellationToken = default)
    {
        using var proc = CreateProcess(command, redirectStandardInput: true);
        proc.StartInfo.Arguments = args;
        await RunCommandProcessAsync(proc, input, cancellationToken);
    }

    public async Task RunCommandAsync(string command, string[] args, string input, CancellationToken cancellationToken = default)
    {
        using var proc = CreateProcess(command, redirectStandardInput: true);
        foreach (var arg in args)
        {
            proc.StartInfo.ArgumentList.Add(arg);
        }

        await RunCommandProcessAsync(proc, input, cancellationToken);
    }

    public async Task WriteInputAndCloseAsync(string command, string args, string input, CancellationToken cancellationToken = default)
    {
        var proc = CreateProcess(command, redirectStandardInput: true, redirectStandardError: false);
        proc.StartInfo.Arguments = args;
        await WriteInputAndCloseProcessAsync(proc, input, cancellationToken);
    }

    public async Task WriteInputAndCloseAsync(string command, string[] args, string input, CancellationToken cancellationToken = default)
    {
        var proc = CreateProcess(command, redirectStandardInput: true, redirectStandardError: false);
        foreach (var arg in args)
        {
            proc.StartInfo.ArgumentList.Add(arg);
        }

        await WriteInputAndCloseProcessAsync(proc, input, cancellationToken);
    }

    private static async Task RunCommandProcessAsync(Process proc, string input, CancellationToken cancellationToken)
    {
        proc.Start();
        var errorTask = proc.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await proc.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
            await proc.StandardInput.FlushAsync(cancellationToken);
        }
        catch
        {
            TryKillProcess(proc);
            throw;
        }

        proc.StandardInput.Close(); 

        await WaitForExitOrKillAsync(proc, cancellationToken);
        var error = await errorTask;
        EnsureSuccessfulExit(proc, error);
    }

    private static async Task WriteInputAndCloseProcessAsync(Process proc, string input, CancellationToken cancellationToken)
    {
        proc.Start();
        try
        {
            await proc.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
            await proc.StandardInput.FlushAsync(cancellationToken);
            proc.StandardInput.Close();
            ObserveProcessExit(proc);
        }
        catch
        {
            TryKillProcess(proc);
            proc.Dispose();
            throw;
        }
    }

    private static void ObserveProcessExit(Process process)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await process.WaitForExitAsync();
            }
            catch
            {
                // Process lifetime observation is best-effort for clipboard owner processes.
            }
            finally
            {
                process.Dispose();
            }
        });
    }

    public async Task ExecuteCommandAsync(string command, string[] args, CancellationToken cancellationToken = default)
    {
        using var proc = CreateProcess(command, redirectStandardInput: false, redirectStandardOutput: true);

        foreach (var arg in args)
        {
            proc.StartInfo.ArgumentList.Add(arg);
        }

        proc.Start();
        var outputTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = proc.StandardError.ReadToEndAsync(cancellationToken);
        await WaitForExitOrKillAsync(proc, cancellationToken);
        await outputTask;
        var error = await errorTask;
        EnsureSuccessfulExit(proc, error);
    }

    public async Task<string> ReadCommandAsync(string command, string args, CancellationToken cancellationToken = default)
    {
        using var proc = CreateProcess(command, redirectStandardInput: false, redirectStandardOutput: true);
        proc.StartInfo.Arguments = args;
        return await ReadCommandProcessAsync(proc, cancellationToken);
    }

    public async Task<string> ReadCommandAsync(string command, string[] args, CancellationToken cancellationToken = default)
    {
        using var proc = CreateProcess(command, redirectStandardInput: false, redirectStandardOutput: true);
        foreach (var arg in args)
        {
            proc.StartInfo.ArgumentList.Add(arg);
        }

        return await ReadCommandProcessAsync(proc, cancellationToken);
    }

    private static async Task<string> ReadCommandProcessAsync(Process proc, CancellationToken cancellationToken)
    {
        proc.Start();
        var resultTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = proc.StandardError.ReadToEndAsync(cancellationToken);
        await WaitForExitOrKillAsync(proc, cancellationToken);
        var result = await resultTask;
        var error = await errorTask;
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
                CreateNoWindow = true
            }
        };
    }

    private static async Task WaitForExitOrKillAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken);
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
        catch
        {
            // Cancellation is best-effort; callers still observe the original failure/cancellation.
        }
    }

    private static void EnsureSuccessfulExit(Process process, string error)
    {
        if (process.ExitCode == 0)
        {
            return;
        }

        var message = string.IsNullOrWhiteSpace(error)
            ? $"Command '{process.StartInfo.FileName}' exited with code {process.ExitCode}."
            : $"Command '{process.StartInfo.FileName}' exited with code {process.ExitCode}: {error.Trim()}";
        throw new InvalidOperationException(message);
    }
}
