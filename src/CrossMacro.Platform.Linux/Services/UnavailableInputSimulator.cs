using System;
using CrossMacro.Core.Services;

namespace CrossMacro.Platform.Linux.Services;

public sealed class UnavailableInputSimulator : IInputSimulator, IInputSimulatorCapabilities
{
    public string ProviderName => "Unavailable (No Linux Input Backend)";

    public bool IsSupported => false;
    public bool SupportsAbsoluteCoordinates => false;

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        throw new InvalidOperationException("No usable Linux input backend is available.");
    }

    public void MoveAbsolute(int x, int y)
    {
        throw new InvalidOperationException("No usable Linux input backend is available.");
    }

    public void MoveRelative(int dx, int dy)
    {
        throw new InvalidOperationException("No usable Linux input backend is available.");
    }

    public void MouseButton(int button, bool pressed)
    {
        throw new InvalidOperationException("No usable Linux input backend is available.");
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
        throw new InvalidOperationException("No usable Linux input backend is available.");
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        throw new InvalidOperationException("No usable Linux input backend is available.");
    }

    public void Sync()
    {
        throw new InvalidOperationException("No usable Linux input backend is available.");
    }

    public void Dispose()
    {
    }
}
