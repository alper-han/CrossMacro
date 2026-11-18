using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.TestDoubles;

/// <summary>Appends one line per input operation to a log shared with the other recording doubles of a test.</summary>
internal sealed class ActivityRecordingInputSimulator(List<string> activity) : IInputSimulator, IInputSimulatorCapabilities
{
    public string ProviderName => "recording-input-simulator";

    public bool IsSupported => true;

    public bool SupportsAbsoluteCoordinates { get; init; } = true;

    public int InitializedWidth { get; private set; }

    public int InitializedHeight { get; private set; }

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        InitializedWidth = screenWidth;
        InitializedHeight = screenHeight;
    }

    public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Initialize(screenWidth, screenHeight);
        return Task.CompletedTask;
    }

    public void MoveAbsolute(int x, int y)
    {
        activity.Add(string.Create(CultureInfo.InvariantCulture, $"input:move-abs:{x},{y}"));
    }

    public void MoveRelative(int dx, int dy)
    {
        activity.Add(string.Create(CultureInfo.InvariantCulture, $"input:move:{dx},{dy}"));
    }

    public void MouseButton(int button, bool pressed)
    {
        if (pressed)
        {
            var name = button switch
            {
                MouseButtonCode.Right => "right",
                MouseButtonCode.Middle => "middle",
                _ => "left",
            };
            activity.Add($"input:click:{name}");
        }
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        activity.Add(string.Create(CultureInfo.InvariantCulture, $"input:key:{keyCode}:{(pressed ? "down" : "up")}"));
    }

    public void Sync()
    {
    }

    public void Dispose()
    {
    }
}
