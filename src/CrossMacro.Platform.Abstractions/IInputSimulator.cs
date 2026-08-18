
namespace CrossMacro.Platform.Abstractions;

public interface IInputSimulator : IDisposable
{
    public string ProviderName { get; }

    public bool IsSupported { get; }

    public void Initialize(int screenWidth = 0, int screenHeight = 0);

    public Task InitializeAsync(
        int screenWidth = 0,
        int screenHeight = 0,
        CancellationToken cancellationToken = default);

    public void MoveAbsolute(int x, int y);

    public void MoveRelative(int dx, int dy);

    public void MouseButton(int button, bool pressed);

    public void Scroll(int delta, bool isHorizontal = false);

    public void KeyPress(int keyCode, bool pressed);

    public void Sync();
}
