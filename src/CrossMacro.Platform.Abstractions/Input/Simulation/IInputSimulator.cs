
namespace CrossMacro.Platform.Abstractions.Input.Simulation;

public interface IInputSimulator : IDisposable
{
    public string ProviderName { get; }

    public bool IsSupported { get; }

    public Task InitializeAsync(
        int screenWidth = 0,
        int screenHeight = 0,
        CancellationToken cancellationToken = default);

    public void MoveAbsolute(int x, int y);

    public void MoveRelative(int dx, int dy);

    public void MouseButton(int button, bool pressed);

    /// <summary>Scrolls by <paramref name="delta"/> whole wheel notches; positive scrolls up, or right when <paramref name="isHorizontal"/>.</summary>
    public void Scroll(int delta, bool isHorizontal = false);

    public void KeyPress(int keyCode, bool pressed);

    public void Sync();
}
