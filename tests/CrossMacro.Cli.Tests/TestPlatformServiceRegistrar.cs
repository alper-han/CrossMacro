using System.Globalization;
using CrossMacro.Platform.Abstractions.Input.Capture;
using CrossMacro.Platform.Abstractions.Input.Keyboard;
using CrossMacro.Platform.Abstractions.Input.Pointer;
using CrossMacro.Platform.Abstractions.Input.Simulation;
using CrossMacro.Platform.Abstractions.Recording.Strategies;

namespace CrossMacro.Cli.Tests;

/// <summary>The smallest platform registration that lets the CLI runtime compose without a real OS backend.</summary>
internal class TestPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public virtual void RegisterPlatformServices(IServiceCollection services)
    {
        _ = services.AddSingleton<IRuntimeContext, TestRuntimeContext>();
        _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
        _ = services.AddSingleton<IEnvironmentInfoProvider, TestEnvironmentInfoProvider>();
        _ = services.AddSingleton<ICoordinateStrategyFactory, TestCoordinateStrategyFactory>();
        _ = services.AddSingleton<IKeyboardLayoutService, TestKeyboardLayoutService>();
        _ = services.AddSingleton<IMousePositionProvider, TestMousePositionProvider>();
        _ = services.AddTransient<Func<IInputSimulator>>(_ => () => new TestInputSimulator());
        _ = services.AddTransient<Func<IInputCapture>>(_ => () => new TestInputCapture());
    }

    private sealed class TestRuntimeContext : IRuntimeContext, IDisplayEnvironmentDiagnostic
    {
        public bool IsLinux => false;
        public bool IsWindows => true;
        public bool IsMacOS => false;
        public bool IsFlatpak => false;
        public string? SessionType => null;
        public string? Display => null;
        public string? WaylandDisplay => null;
    }

    private sealed class TestEnvironmentInfoProvider : IEnvironmentInfoProvider
    {
        public DisplayEnvironment CurrentEnvironment => DisplayEnvironment.Windows;
        public bool WindowManagerHandlesCloseButton => false;
    }

    private sealed class TestCoordinateStrategyFactory : ICoordinateStrategyFactory
    {
        public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero, bool useLogicalRelativeCoordinates)
        {
            return new RelativeCoordinateStrategy();
        }
    }

    private sealed class TestKeyboardLayoutService : IKeyboardLayoutService
    {
        public string GetKeyName(int keyCode) => keyCode.ToString(CultureInfo.InvariantCulture);

        public int GetKeyCode(string keyName) => int.TryParse(keyName, CultureInfo.InvariantCulture, out var keyCode) ? keyCode : 0;

        public char? GetCharFromKeyCode(int keyCode, bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock) => null;

        public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c) => null;
    }

    private sealed class TestMousePositionProvider : IMousePositionProvider
    {
        public string ProviderName => "test-position";
        public bool IsSupported => true;
        public Task<(int X, int Y)?> GetAbsolutePositionAsync() => Task.FromResult<(int X, int Y)?>((100, 100));
        public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>((1920, 1080));

        public void Dispose()
        {
        }
    }

    private sealed class TestInputSimulator : IInputSimulator
    {
        public string ProviderName => "test-sim";
        public bool IsSupported => true;

        public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public void MoveAbsolute(int x, int y) { }
        public void MoveRelative(int dx, int dy) { }
        public void MouseButton(int button, bool pressed) { }
        public void Scroll(int delta, bool isHorizontal = false) { }
        public void KeyPress(int keyCode, bool pressed) { }
        public void Sync() { }
        public void Dispose() { }
    }

    private sealed class TestInputCapture : IInputCapture
    {
        public string ProviderName => "test-cap";
        public bool IsSupported => true;
        public event EventHandler<CapturedInputEventArgs>? InputReceived { add { } remove { } }
        public event EventHandler<InputCaptureErrorEventArgs>? CaptureError { add { } remove { } }
        public void Configure(bool captureMouse, bool captureKeyboard) { }
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public void StopCapture() { }
        public void Dispose() { }
    }
}
