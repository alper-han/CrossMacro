using System;
using System.IO;
using System.Linq;
using CrossMacro.Daemon.Contracts.Ipc;

namespace CrossMacro.Platform.Linux.Services;

internal static class LinuxInputProbeUtilities
{
    internal static string? ResolveAvailableSocketPath(Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        return fileExists(IpcProtocol.DefaultSocketPath)
            ? IpcProtocol.DefaultSocketPath
            : null;
    }

    internal static bool HasUInputWriteAccess(Func<string, bool> canOpenForWrite)
    {
        ArgumentNullException.ThrowIfNull(canOpenForWrite);

        return canOpenForWrite(LinuxConstants.UInputDevicePath) ||
               canOpenForWrite(LinuxConstants.UInputAlternatePath);
    }

    internal static bool HasReadableInputEventAccess(
        Func<string, bool> canOpenForRead,
        Func<string[]> getInputEventCandidates)
    {
        ArgumentNullException.ThrowIfNull(canOpenForRead);
        ArgumentNullException.ThrowIfNull(getInputEventCandidates);

        var eventDevices = getInputEventCandidates();
        if (eventDevices.Length == 0)
        {
            return false;
        }

        return eventDevices.Any(canOpenForRead);
    }

    internal static LinuxDaemonHandshakeTransport.ProbeResult ProbeDaemonHandshakeTransportWithinBudget(string socketPath, TimeSpan timeout)
    {
        return LinuxDaemonHandshakeTransport.ProbeWithinBudget(socketPath, timeout);
    }

    internal static bool CanOpenForWrite(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using (File.OpenWrite(path))
            {
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    internal static bool CanOpenForRead(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static string[] GetInputEventCandidates()
    {
        try
        {
            if (!Directory.Exists("/dev/input"))
            {
                return [];
            }

            return Directory.GetFiles("/dev/input", "event*");
        }
        catch
        {
            return [];
        }
    }
}
