using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.UI.Services;

public sealed class ExternalUrlOpener : IExternalUrlOpener
{
    private readonly Func<ProcessStartInfo, LaunchResult> _tryStart;
    private readonly Func<string, bool> _commandExists;
    private readonly IRuntimeContext _runtimeContext;

    public ExternalUrlOpener()
        : this(new RuntimeContext())
    {
    }

    public ExternalUrlOpener(IRuntimeContext runtimeContext)
        : this(runtimeContext, TryStartProcess, CommandExists)
    {
    }

    internal ExternalUrlOpener(
        IRuntimeContext runtimeContext,
        Func<ProcessStartInfo, LaunchResult> tryStart,
        Func<string, bool> commandExists)
    {
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        _tryStart = tryStart ?? throw new ArgumentNullException(nameof(tryStart));
        _commandExists = commandExists ?? throw new ArgumentNullException(nameof(commandExists));
    }

    public void Open(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Only absolute HTTP and HTTPS URLs can be opened.", nameof(url));
        }

        List<Exception> failures = [];
        foreach (var startInfo in CreateStartInfos(url, _runtimeContext, _commandExists))
        {
            try
            {
                var result = _tryStart(startInfo);
                if (result.Success)
                {
                    return;
                }

                if (result.Failure is not null)
                {
                    failures.Add(result.Failure);
                }
            }
            catch (Win32Exception ex) when (IsCommandNotFound(ex))
            {
                // A command can disappear between the PATH check and Process.Start.
                // Missing optional Linux fallback commands should not obscure the real opener failure.
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                failures.Add(ex);
            }
        }

        throw CreateOpenFailedException(failures);
    }

    private static IEnumerable<ProcessStartInfo> CreateStartInfos(
        string url,
        IRuntimeContext runtimeContext,
        Func<string, bool> commandExists)
    {
        if (!runtimeContext.IsLinux || runtimeContext.IsFlatpak)
        {
            yield return new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };

            yield break;
        }

        foreach (var command in GetLinuxFallbackCommands())
        {
            if (commandExists(command.FileName))
            {
                yield return CreateCommand(command.FileName, url, command.ArgumentsBeforeUrl);
            }
        }
    }

    private static IEnumerable<LinuxOpenCommand> GetLinuxFallbackCommands()
    {
        yield return new LinuxOpenCommand("xdg-open");
        yield return new LinuxOpenCommand("gio", "open");
        yield return new LinuxOpenCommand("sensible-browser");
    }

    private static ProcessStartInfo CreateCommand(string fileName, string url, IReadOnlyList<string> argumentsBeforeUrl)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var argument in argumentsBeforeUrl)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(url);
        return startInfo;
    }

    private static LaunchResult TryStartProcess(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return LaunchResult.Failed(new InvalidOperationException($"Launcher '{startInfo.FileName}' did not start."));
        }

        var standardErrorTask = startInfo.RedirectStandardError
            ? process.StandardError.ReadToEndAsync()
            : null;
        var standardOutputTask = startInfo.RedirectStandardOutput
            ? process.StandardOutput.ReadToEndAsync()
            : null;

        if (!process.WaitForExit(2000))
        {
            return LaunchResult.Succeeded;
        }

        if (process.ExitCode == 0)
        {
            return LaunchResult.Succeeded;
        }

        var error = standardErrorTask?.GetAwaiter().GetResult().Trim() ?? string.Empty;
        _ = standardOutputTask?.GetAwaiter().GetResult();
        var message = string.IsNullOrWhiteSpace(error)
            ? $"Launcher '{startInfo.FileName}' exited with code {process.ExitCode}."
            : $"Launcher '{startInfo.FileName}' exited with code {process.ExitCode}: {error}";
        return LaunchResult.Failed(new InvalidOperationException(message));
    }

    private static InvalidOperationException CreateOpenFailedException(IReadOnlyCollection<Exception> failures)
    {
        const string message = "Unable to open the URL with the available desktop launchers.";
        return failures.Count == 0
            ? new InvalidOperationException(message)
            : new InvalidOperationException(message, new AggregateException(failures));
    }

    private static bool CommandExists(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.Contains(Path.DirectorySeparatorChar)
            || fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(fileName);
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, fileName))
            .Any(File.Exists);
    }

    private static bool IsCommandNotFound(Win32Exception exception)
    {
        return exception.NativeErrorCode == 2;
    }

    internal readonly record struct LaunchResult(bool Success, Exception? Failure)
    {
        public static LaunchResult Succeeded { get; } = new(true, null);

        public static LaunchResult Failed(Exception failure)
        {
            return new LaunchResult(false, failure ?? throw new ArgumentNullException(nameof(failure)));
        }
    }

    private readonly record struct LinuxOpenCommand(string FileName, params string[] ArgumentsBeforeUrl);
}
