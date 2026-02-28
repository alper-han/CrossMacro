using System;

namespace CrossMacro.Cli.Services;

public interface IHeadlessHotkeyActionService : IDisposable
{
    bool IsRunning { get; }

    void Start();

    void Stop();
}
