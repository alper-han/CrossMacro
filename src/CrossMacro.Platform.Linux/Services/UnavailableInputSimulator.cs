using System;
using CrossMacro.Core.Services;

namespace CrossMacro.Platform.Linux.Services;

public sealed class UnavailableInputSimulator : IInputSimulator, IInputSimulatorCapabilities
{
    public const string DefaultFailureMessage = "No usable Linux input backend is available.";

    public UnavailableInputSimulator(string? failureMessage = null)
    {
        FailureMessage = string.IsNullOrWhiteSpace(failureMessage)
            ? DefaultFailureMessage
            : failureMessage;
    }

    public string ProviderName => "Unavailable (No Linux Input Backend)";

    public string FailureMessage { get; }

    public bool IsSupported => false;
    public bool SupportsAbsoluteCoordinates => false;

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    public void MoveAbsolute(int x, int y)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    public void MoveRelative(int dx, int dy)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    public void MouseButton(int button, bool pressed)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    public void Sync()
    {
        throw new InvalidOperationException(FailureMessage);
    }

    public void Dispose()
    {
    }
}
