namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

internal class TestInputSimulator : IInputSimulator
{
    public List<int> PressedKeys { get; } = new();

    public string ProviderName => "test";

    public bool IsSupported => true;

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
    }

    public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Initialize(screenWidth, screenHeight);
        return Task.CompletedTask;
    }

    public void MoveAbsolute(int x, int y)
    {
    }

    public void MoveRelative(int dx, int dy)
    {
    }

    public void MouseButton(int button, bool pressed)
    {
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
    }

    public virtual void KeyPress(int keyCode, bool pressed)
    {
        if (pressed)
        {
            PressedKeys.Add(keyCode);
        }
    }

    public void Sync()
    {
    }

    public void Dispose()
    {
    }
}
