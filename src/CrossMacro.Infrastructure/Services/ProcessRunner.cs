using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace CrossMacro.Infrastructure.Services;

public class ProcessRunner : IProcessRunner
{
    public async Task<bool> CheckCommandAsync(string command)
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
            
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task RunCommandAsync(string command, string args, string input)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        proc.Start();
        await proc.StandardInput.WriteAsync(input);
        proc.StandardInput.Close(); 
        
        await proc.WaitForExitAsync();
    }

    public async Task ExecuteCommandAsync(string command, string[] args)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var arg in args)
        {
            proc.StartInfo.ArgumentList.Add(arg);
        }

        proc.Start();
        await proc.WaitForExitAsync();
    }

    public async Task<string> ReadCommandAsync(string command, string args)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        proc.Start();
        var result = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return result;
    }
}
