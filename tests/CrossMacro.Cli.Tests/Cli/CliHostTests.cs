using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CoreLogging = CrossMacro.Core.Logging;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Cli;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Cli.Tests;

public class CliHostTests
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

            var host = new CliHost(new MinimalPlatformServiceRegistrar());
            var exitCode = await host.RunAsync(new SettingsGetCliOptions(JsonOutput: true));

            Assert.True(exitCode == (int)CliExitCode.Success, $"Unexpected exit code: {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
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

            var host = new CliHost(new SettingsOnlyPlatformServiceRegistrar());
            var exitCode = await host.RunAsync(new SettingsGetCliOptions(JsonOutput: true));

            Assert.True(exitCode == (int)CliExitCode.Success, $"Unexpected exit code: {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
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

            var host = new CliHost(new SettingsOnlyPlatformServiceRegistrar());
            var exitCode = await host.RunAsync(new SettingsGetCliOptions(JsonOutput: true));

            Assert.True(exitCode == (int)CliExitCode.Success, $"Unexpected exit code: {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
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
        public void Error(string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Error(Exception exception, string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Fatal(string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);
        public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) => Write(messageTemplate);

        private static void Write(string messageTemplate)
        {
            Console.Error.WriteLine($"preconfigured noisy logger: {messageTemplate}");
        }
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

            var host = new CliHost(new ThrowingPlatformServiceRegistrar());
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

            var host = new CliHost(new CancelledPlatformServiceRegistrar());
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
        services.AddCrossMacroCliRuntimeServices(new MinimalPlatformServiceRegistrar(), CliRuntimeProfile.OneShot);
        services.AddCliServices();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<CliCommandHandlerResolver>(provider.GetRequiredService<ICliCommandHandlerResolver>());
        Assert.NotNull(provider.GetRequiredService<CliCommandExecutor>());
    }

    private sealed class ThrowingPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public PlatformClipboardRegistration ClipboardRegistration => PlatformClipboardRegistration.Default;

        public void RegisterPlatformServices(IServiceCollection services)
        {
            throw new InvalidOperationException("simulated registration failure");
        }
    }

    private sealed class SettingsOnlyPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public PlatformClipboardRegistration ClipboardRegistration => PlatformClipboardRegistration.Default;

        public void RegisterPlatformServices(IServiceCollection services)
        {
            // Intentionally empty: settings commands should not force resolution of platform services.
        }
    }

    private sealed class CancelledPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public PlatformClipboardRegistration ClipboardRegistration => PlatformClipboardRegistration.Default;

        public void RegisterPlatformServices(IServiceCollection services)
        {
            throw new OperationCanceledException("simulated cancellation");
        }
    }

    private sealed class MinimalPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public PlatformClipboardRegistration ClipboardRegistration => PlatformClipboardRegistration.Default;

        public void RegisterPlatformServices(IServiceCollection services)
        {
            services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            services.AddSingleton<IEnvironmentInfoProvider, TestEnvironmentInfoProvider>();
            services.AddSingleton<ICoordinateStrategyFactory, TestCoordinateStrategyFactory>();
            services.AddSingleton<IKeyboardLayoutService, TestKeyboardLayoutService>();
            services.AddSingleton<IMousePositionProvider, TestMousePositionProvider>();
            services.AddTransient<Func<IInputSimulator>>(_ => () => new TestInputSimulator());
            services.AddTransient<Func<IInputCapture>>(_ => () => new TestInputCapture());
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
        public string GetKeyName(int keyCode) => keyCode.ToString();
        public int GetKeyCode(string keyName) => int.TryParse(keyName, out var keyCode) ? keyCode : 0;
        public char? GetCharFromKeyCode(int keyCode, bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock) => null;
        public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c) => null;
    }

    private sealed class TestMousePositionProvider : IMousePositionProvider
    {
        public string ProviderName => "test-position";
        public bool IsSupported => true;
        public Task<(int X, int Y)?> GetAbsolutePositionAsync() => Task.FromResult<(int X, int Y)?>( (100, 100) );
        public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>( (1920, 1080) );
        public void Dispose()
        {
        }
    }

    private sealed class TestInputSimulator : IInputSimulator
    {
        public string ProviderName => "test-sim";
        public bool IsSupported => true;
        public void Initialize(int screenWidth = 0, int screenHeight = 0) { }
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
#pragma warning disable CS0067
        public event EventHandler<InputCaptureEventArgs>? InputReceived;
        public event EventHandler<string>? Error;
#pragma warning restore CS0067
        public void Configure(bool captureMouse, bool captureKeyboard) { }
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public void Stop() { }
        public void Dispose() { }
    }
}
