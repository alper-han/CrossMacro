using System;
using System.Diagnostics;

namespace CrossMacro.UI.Services;

public sealed class ExternalUrlOpener : IExternalUrlOpener
{
    public void Open(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
