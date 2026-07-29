
namespace CrossMacro.Platform.Linux.Tests.Services.Keyboard;

public sealed class LinuxKeyboardLayoutServiceTests : IDisposable
{
    private readonly LinuxKeyboardLayoutService _service;

    public LinuxKeyboardLayoutServiceTests()
    {
        // On non-Linux (e.g. CI environments without X) this might log errors but should not throw.
        // We rely on fallback logic which is what we are testing here mainly.
        var layoutDetector = new LinuxLayoutDetector();
        var xkbState = new XkbStateManager();
        var keyMapper = new LinuxKeyCodeMapper(xkbState);
        _service = new LinuxKeyboardLayoutService(layoutDetector, keyMapper, xkbState);
    }

    [Fact]
    public void GetKeyName_ReturnsCorrectFallback_ForStandardKeys()
    {
        // Assert
        Assert.Equal("A", _service.GetKeyName(30)); // A
        Assert.Equal("Space", _service.GetKeyName(57)); // Space
        Assert.Equal("Enter", _service.GetKeyName(28)); // Enter
    }

    [Fact]
    public void GetKeyCode_ReturnsCorrectFallback_ForStandardNames()
    {
        // Assert
        Assert.Equal(30, _service.GetKeyCode("A"));
        Assert.Equal(57, _service.GetKeyCode("Space"));
        Assert.Equal(29, _service.GetKeyCode("Ctrl"));
    }

    public void Dispose() => _service.Dispose();

    [Fact]
    public async Task Dispose_WhenDetectorIgnoresCancellation_ReturnsPromptlyAndPreventsPostDisposalInitialization()
    {
        var detector = new DelayedLayoutDetector();
        var xkbState = new DisposalAwareXkbStateManager();
        var service = new LinuxKeyboardLayoutService(
            detector,
            Substitute.For<ILinuxKeyCodeMapper>(),
            xkbState);

        await detector.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var disposeTask = Task.Run(service.Dispose);
        var disposeCompletedPromptly = await Task.WhenAny(
            disposeTask,
            Task.Delay(TimeSpan.FromSeconds(1)));

        detector.Fail(new InvalidOperationException("Controlled detector failure"));
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(50);

        Assert.Same(disposeTask, disposeCompletedPromptly);
        Assert.False(xkbState.InitializeAfterDispose);
    }

    private sealed class DelayedLayoutDetector : ILinuxLayoutDetector
    {
        private readonly TaskCompletionSource<string?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string?> DetectLayoutAsync(CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            return _completion.Task;
        }

        public void Fail(Exception exception) => _completion.SetException(exception);
    }

    private sealed class DisposalAwareXkbStateManager : IXkbStateManager
    {
        private bool _disposed;

        public bool InitializeAfterDispose { get; private set; }

        public bool IsInitialized => false;

        public void Initialize(string? layout)
        {
            InitializeAfterDispose = _disposed;
        }

        public string? GetUtf8String(uint keycode) => null;

        public char? GetCharFromKeyCode(int keyCode, bool shift, bool altGr, bool capsLock) => null;

        public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c) => null;

        public void Dispose() => _disposed = true;
    }
}
