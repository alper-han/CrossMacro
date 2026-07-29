
using System.Globalization;

namespace CrossMacro.Cli.Tests;

public sealed class CliHostTests
{
    [Fact]
    public async Task RunAsync_WhenSettingsGetWithJson_ReturnsSuccess()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            LoggerSetup.Initialize("Fatal");
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var registrar = new MinimalPlatformServiceRegistrar();
            var host = new CliHost(
                ConfigureNoOpCliServices(registrar),
                (services, profile) => services.AddCrossMacroCliRuntimeServices(registrar, profile),
                static options =>
                {
                    if (options.JsonOutput)
                    {
                        LoggerSetup.Initialize("Fatal", enableFileLogging: false, enableConsoleLogging: false);
                    }
                });
            var exitCode = await host.RunAsync(new SettingsGetCliOptions(JsonOutput: true));

            Assert.Equal((int)CliExitCode.Success, exitCode);
            Assert.Contains("\"status\": \"ok\"", stdout.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_WhenSettingsGetWithJson_AndMinimalPlatformRegistrations_ReturnsSuccess()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            LoggerSetup.Initialize("Fatal");
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var registrar = new SettingsOnlyPlatformServiceRegistrar();
            var host = new CliHost(
                ConfigureNoOpCliServices(registrar),
                (services, profile) => services.AddCrossMacroCliRuntimeServices(registrar, profile),
                static options =>
                {
                    if (options.JsonOutput)
                    {
                        LoggerSetup.Initialize("Fatal", enableFileLogging: false, enableConsoleLogging: false);
                    }
                });
            var exitCode = await host.RunAsync(new SettingsGetCliOptions(JsonOutput: true));

            Assert.Equal((int)CliExitCode.Success, exitCode);
            Assert.Contains("\"status\": \"ok\"", stdout.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_WhenSettingsGetWithJson_AndBootstrapLoggerWasActive_ReturnsCleanJsonOutput()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        using var loggerScope = CoreLogging.Log.PushLogger(new StderrCoreLogger());
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var registrar = new SettingsOnlyPlatformServiceRegistrar();
            var host = new CliHost(
                ConfigureNoOpCliServices(registrar),
                (services, profile) => services.AddCrossMacroCliRuntimeServices(registrar, profile),
                static options =>
                {
                    if (options.JsonOutput)
                    {
                        LoggerSetup.Initialize("Fatal", enableFileLogging: false, enableConsoleLogging: false);
                    }
                });
            var exitCode = await host.RunAsync(new SettingsGetCliOptions(JsonOutput: true));

            Assert.Equal((int)CliExitCode.Success, exitCode);
            Assert.Contains("\"status\": \"ok\"", stdout.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("preconfigured noisy logger", stderr.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed class StderrCoreLogger : CoreLogging.ICoreLogger
    {
        public bool IsEnabled(CoreLogging.CoreLogLevel level) => true;

        public void Verbose(string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Debug(string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Information(string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Information(Exception exception, string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Warning(string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void LogError(string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void LogError(Exception exception, string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Fatal(string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);

        private static void Write(string messageTemplate)
        {
            Console.Error.WriteLine($"preconfigured noisy logger: {messageTemplate}");
        }
    }

    private static Action<IServiceCollection> ConfigureNoOpCliServices(IPlatformServiceRegistrar registrar)
    {
        return services =>
        {
            registrar.RegisterPlatformServices(services);
            services.TryAddSingleton<IKeyboardLayoutService, TestKeyboardLayoutService>();
            _ = services.AddSingleton<IClipboardService, CrossMacro.Cli.Services.NoOpClipboardService>();
            _ = services.AddSingleton<IImageClipboardService, CrossMacro.Cli.Services.NoOpImageClipboardService>();
        };
    }

    [Fact]
    public async Task RunAsync_WhenRuntimeExceptionOccurs_ReturnsRuntimeErrorAsJson()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            LoggerSetup.Initialize("Fatal");
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var registrar = new ThrowingPlatformServiceRegistrar();
            var host = new CliHost(
                services => registrar.RegisterPlatformServices(services),
                (services, profile) => services.AddCrossMacroCliRuntimeServices(registrar, profile));
            var exitCode = await host.RunAsync(new DoctorCliOptions(JsonOutput: true));

            Assert.Equal((int)CliExitCode.RuntimeError, exitCode);
            Assert.Contains("\"status\": \"error\"", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"code\": 6", stdout.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_WhenCancelledDuringBootstrap_ReturnsCancelledAsJson()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            LoggerSetup.Initialize("Fatal");
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var registrar = new CancelledPlatformServiceRegistrar();
            var host = new CliHost(
                services => registrar.RegisterPlatformServices(services),
                (services, profile) => services.AddCrossMacroCliRuntimeServices(registrar, profile));
            var exitCode = await host.RunAsync(new DoctorCliOptions(JsonOutput: true));

            Assert.Equal((int)CliExitCode.Cancelled, exitCode);
            Assert.Contains("\"status\": \"error\"", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"code\": 130", stdout.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void AddCliServices_RegistersCommandHandlerResolverAndExecutor()
    {
        var services = new ServiceCollection();
        _ = services.AddCrossMacroCliRuntimeServices(new MinimalPlatformServiceRegistrar(), CliRuntimeProfile.OneShot);
        _ = services.AddCliServices();

        using var provider = services.BuildServiceProvider();

        _ = Assert.IsType<CliCommandHandlerResolver>(provider.GetRequiredService<ICliCommandHandlerResolver>());
        Assert.NotNull(provider.GetRequiredService<CliCommandExecutor>());
    }

    private sealed class ThrowingPlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            throw new InvalidOperationException("simulated registration failure");
        }
    }

    private sealed class SettingsOnlyPlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            // Intentionally empty: settings commands should not force resolution of platform services.
        }
    }

    private sealed class CancelledPlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            throw new OperationCanceledException("simulated cancellation");
        }
    }

    private sealed class MinimalPlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            _ = services.AddSingleton<IEnvironmentInfoProvider, TestEnvironmentInfoProvider>();
            _ = services.AddSingleton<ICoordinateStrategyFactory, TestCoordinateStrategyFactory>();
            _ = services.AddSingleton<IKeyboardLayoutService, TestKeyboardLayoutService>();
            _ = services.AddSingleton<IMousePositionProvider, TestMousePositionProvider>();
            _ = services.AddTransient<Func<IInputSimulator>>(_ => () => new TestInputSimulator());
            _ = services.AddTransient<Func<IInputCapture>>(_ => () => new TestInputCapture());
        }
    }

    private sealed class TestEnvironmentInfoProvider : IEnvironmentInfoProvider
    {
        public DisplayEnvironment CurrentEnvironment => DisplayEnvironment.Windows;
        public bool WindowManagerHandlesCloseButton => false;
    }

    private sealed class TestCoordinateStrategyFactory : ICoordinateStrategyFactory
    {
        public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero)
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
        public void Initialize(int screenWidth = 0, int screenHeight = 0) { }
        public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Initialize(screenWidth, screenHeight);
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
