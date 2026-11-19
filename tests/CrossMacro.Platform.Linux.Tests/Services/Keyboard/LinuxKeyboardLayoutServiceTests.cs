
namespace CrossMacro.Platform.Linux.Tests.Services.Keyboard;

public sealed class LinuxKeyboardLayoutServiceTests : IDisposable
{
    private readonly LinuxKeyboardLayoutService _service;

    public LinuxKeyboardLayoutServiceTests()
    {
        var layoutDetector = new CompletedLayoutDetector();
        var xkbState = new NoOpXkbStateManager();
        var keyMapper = new LinuxKeyCodeMapper();
        _service = new LinuxKeyboardLayoutService(layoutDetector, keyMapper, xkbState);
    }

    [Fact]
    public void GetKeyName_ReturnsCorrectFallback_ForStandardKeys()
    {
        Assert.Equal("A", _service.GetKeyName(30));
        Assert.Equal("Space", _service.GetKeyName(57));
        Assert.Equal("Enter", _service.GetKeyName(28));
    }

    [Fact]
    public void GetKeyCode_ReturnsCorrectFallback_ForStandardNames()
    {
        Assert.Equal(30, _service.GetKeyCode("A"));
        Assert.Equal(57, _service.GetKeyCode("Space"));
        Assert.Equal(29, _service.GetKeyCode("Ctrl"));
    }

        [Theory]
    [InlineData(29, "Ctrl")]
    [InlineData(97, "Ctrl")]
    [InlineData(42, "Shift")]
    [InlineData(54, "Shift")]
    [InlineData(56, "Alt")]
    [InlineData(100, "AltGr")]
    [InlineData(125, "Super")]
    public void GetKeyName_ShouldReturnCorrectModifierKeys(int keyCode, string expectedName)
    {
        Assert.Equal(expectedName, _service.GetKeyName(keyCode));
    }

    [Theory]
    [InlineData(59, "F1")]
    [InlineData(68, "F10")]
    [InlineData(87, "F11")]
    [InlineData(88, "F12")]
    [InlineData(183, "F13")]
    [InlineData(194, "F24")]
    [InlineData(82, "Numpad0")]
    [InlineData(79, "Numpad1")]
    [InlineData(80, "Numpad2")]
    [InlineData(81, "Numpad3")]
    [InlineData(75, "Numpad4")]
    [InlineData(76, "Numpad5")]
    [InlineData(77, "Numpad6")]
    [InlineData(71, "Numpad7")]
    [InlineData(72, "Numpad8")]
    [InlineData(73, "Numpad9")]
    [InlineData(74, "Numpad-")]
    [InlineData(78, "Numpad+")]
    [InlineData(55, "Numpad*")]
    [InlineData(98, "Numpad/")]
    [InlineData(96, "NumpadEnter")]
    [InlineData(83, "Numpad.")]
    [InlineData(117, "Numpad=")]
    [InlineData(69, "NumLock")]
    [InlineData(70, "ScrollLock")]
    [InlineData(58, "CapsLock")]
    [InlineData(99, "PrintScreen")]
    [InlineData(119, "Pause")]
    public void GetKeyName_ShouldRoundTripNamedKeys(int keyCode, string expectedName)
    {
        Assert.Equal(expectedName, _service.GetKeyName(keyCode));
        Assert.Equal(keyCode, _service.GetKeyCode(expectedName));
    }

    [Fact]
    public void LinuxKeyCodeRegistry_AllKeyNames_DoesNotExposeMutableRegistry()
    {
        var names = LinuxKeyCodeRegistry.AllKeyNames;

        Assert.IsAssignableFrom<IReadOnlyDictionary<int, string>>(names);
        var mutableView = Assert.IsAssignableFrom<IDictionary<int, string>>(names);
        _ = Assert.Throws<NotSupportedException>(() => mutableView.Add(999, "MUTATED"));
        Assert.Equal("KEY_ESC", names[1]);
    }

    public void Dispose() => _service.Dispose();

    [Fact]
    public async Task Dispose_WhenDetectorIgnoresCancellation_DoesNotWaitAndPreventsPostDisposalInitialization()
    {
        var detector = new DelayedLayoutDetector();
        var xkbState = new DisposalAwareXkbStateManager();
        var service = new LinuxKeyboardLayoutService(
            detector,
            Substitute.For<ILinuxKeyCodeMapper>(),
            xkbState);

        await detector.Started.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);
        var disposeTask = Task.Run(service.Dispose, CancellationToken.None);

        // The detector is still pending and ignores cancellation: disposal must not wait for it.
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
        await xkbState.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);
        detector.Fail(new InvalidOperationException("Controlled detector failure"));
        await service.InitializationTask.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);

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

    private sealed class CompletedLayoutDetector : ILinuxLayoutDetector
    {
        public Task<string?> DetectLayoutAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("us");
    }

    private sealed class NoOpXkbStateManager : IXkbStateManager
    {
        public bool IsInitialized => false;

        public int Generation => 0;

        public void Initialize(string? layout) { }

        public string? GetUtf8String(uint keycode) => null;

        public char? GetCharFromKeyCode(int keyCode, bool shift, bool altGr, bool capsLock) => null;

        public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c) => null;

        public void Dispose() { }
    }

    private sealed class DisposalAwareXkbStateManager : IXkbStateManager
    {
        private bool _disposed;

        public bool InitializeAfterDispose { get; private set; }

        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsInitialized => false;

        public int Generation => 0;

        public void Initialize(string? layout)
        {
            InitializeAfterDispose = _disposed;
        }

        public string? GetUtf8String(uint keycode) => null;

        public char? GetCharFromKeyCode(int keyCode, bool shift, bool altGr, bool capsLock) => null;

        public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c) => null;

        public void Dispose()
        {
            _disposed = true;
            _ = Disposed.TrySetResult();
        }
    }
}
