using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrossMacro.CI;

internal static class CISupport
{
    public static string FindRepositoryRoot(string? explicitRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var resolved = Path.GetFullPath(explicitRoot);
            if (!Directory.Exists(resolved))
            {
                throw new DirectoryNotFoundException($"repository root not found: {resolved}");
            }

            return resolved;
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VERSION"))
                && Directory.Exists(Path.Combine(current.FullName, ".github", "workflows")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("repository root could not be discovered; pass --repo-root");
    }

    public static string ResolvePath(string path, string baseDirectory)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));
    }

    public static string RelativePath(string path, string root)
    {
        return Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    public static string ReadText(string path, bool replaceInvalid = false)
    {
        return File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: !replaceInvalid));
    }

    public static bool ParseBoolean(string? value, string option)
    {
        if (value is null)
        {
            throw new ArgumentException($"--{option} requires true or false");
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "y" or "on" => true,
            "false" or "0" or "no" or "n" or "off" => false,
            _ => throw new ArgumentException($"expected true or false for --{option}, got '{value}'"),
        };
    }

    public static int PrintResult(string successMessage, string failureTitle, IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            Console.WriteLine($"OK: {successMessage}");
            return 0;
        }

        Console.WriteLine($"FAIL: {failureTitle}");
        foreach (var error in errors)
        {
            Console.WriteLine($"- {error}");
        }

        return 1;
    }

    public static List<string> ReadLines(string path, bool replaceInvalid = false)
    {
        return ReadText(path, replaceInvalid).Split('\n').Select(line => line.TrimEnd('\r')).ToList();
    }

    public static (int ExitCode, string Output) RunProcess(string fileName, IEnumerable<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"could not start process: {fileName}");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdout, stderr);
        var output = string.Join(Environment.NewLine, new[] { stdout.Result, stderr.Result }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        return (process.ExitCode, output);
    }

    public static string? FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
