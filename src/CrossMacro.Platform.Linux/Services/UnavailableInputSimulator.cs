
namespace CrossMacro.Platform.Linux.Services;

public sealed class UnavailableInputSimulator(string? failureMessage = null) : IInputSimulator, IInputSimulatorCapabilities
{
    public const string DefaultFailureMessage = "No usable Linux input backend is available.";

    public string ProviderName => "Unavailable (No Linux Input Backend)";

    public string FailureMessage { get; } = string.IsNullOrWhiteSpace(failureMessage)
            ? DefaultFailureMessage
            : failureMessage;

    public bool IsSupported => false;
    public bool SupportsAbsoluteCoordinates => false;

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Initialize(screenWidth, screenHeight);
        return Task.CompletedTask;
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

    public void Dispose() { /* Empty */ }
}
