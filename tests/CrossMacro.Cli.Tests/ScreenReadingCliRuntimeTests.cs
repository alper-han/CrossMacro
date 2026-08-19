
using System.Globalization;

namespace CrossMacro.Cli.Tests;

public sealed class ScreenReadingCliRuntimeTests
{
    [Fact]
    public async Task ScreenReading_PixelColor_ResolvesReaderThroughCliRuntimeDi()
    {
        var screenReader = new RecordingScreenPixelReader();

        await using var provider = BuildProvider(screenReader);
        var resolved = provider.GetRequiredService<IScreenPixelReader>();
        var runService = provider.GetRequiredService<IRunScriptExecutionService>();

        var result = await runService.ExecuteAsync(new CliRunExecutionRequest
        {
            Steps = ["pixelcolor 500 300 mycolor"],
        }, CancellationToken.None);

        Assert.Same(screenReader, resolved);
        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal([new ScreenPoint(500, 300)], screenReader.GetPixelPoints);
        Assert.Empty(screenReader.WaitCalls);
        Assert.Empty(screenReader.SearchCalls);
    }

    [Fact]
    public async Task ScreenReading_WaitColor_UsesInjectedScreenPixelReader()
    {
        var screenReader = new RecordingScreenPixelReader();

        await using var provider = BuildProvider(screenReader);
        var runService = provider.GetRequiredService<IRunScriptExecutionService>();

        var result = await runService.ExecuteAsync(new CliRunExecutionRequest
        {
            Steps = ["waitcolor 500 300 00FF00 5000"],
        }, CancellationToken.None);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        var call = Assert.Single(screenReader.WaitCalls);
        Assert.Equal(new ScreenPoint(500, 300), call.Point);
        Assert.Equal(new ScreenPixelColor(0x00, 0xFF, 0x00), call.Expected);
        Assert.Equal(TimeSpan.FromMilliseconds(5000), call.Options.Timeout);
    }

    [Fact]
    public async Task ScreenReading_PixelSearch_UsesInjectedScreenPixelReader()
    {
        var screenReader = new RecordingScreenPixelReader
        {
            SearchMatch = new ScreenPixelSearchMatch(new ScreenPoint(3, 4), new ScreenPixelColor(0x10, 0x20, 0x30)),
        };

        await using var provider = BuildProvider(screenReader);
        var runService = provider.GetRequiredService<IRunScriptExecutionService>();

        var result = await runService.ExecuteAsync(new CliRunExecutionRequest
        {
            Steps = ["pixelsearch 0 0 1920 1080 FF0000 found_x found_y"],
        }, CancellationToken.None);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        var call = Assert.Single(screenReader.SearchCalls);
        Assert.Equal(new ScreenRect(0, 0, 1920, 1080), call.Region);
        Assert.Equal(new ScreenPixelColor(0xFF, 0x00, 0x00), call.Expected);
        Assert.Equal(0, call.Tolerance);
    }

    [Fact]
    public async Task ScreenReading_PixelSearch_WithTolerancePassesVariationToReader()
    {
        var screenReader = new RecordingScreenPixelReader
        {
            SearchMatch = new ScreenPixelSearchMatch(new ScreenPoint(3, 4), new ScreenPixelColor(0x10, 0x20, 0x30)),
        };

        await using var provider = BuildProvider(screenReader);
        var runService = provider.GetRequiredService<IRunScriptExecutionService>();

        var result = await runService.ExecuteAsync(new CliRunExecutionRequest
        {
            Steps = ["pixelsearch 0 0 1920 1080 FF0000 found_x found_y tolerance 26"],
        }, CancellationToken.None);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        var call = Assert.Single(screenReader.SearchCalls);
        Assert.Equal(26, call.Tolerance);
    }

    [Fact]
    public async Task ScreenReading_InvalidSyntax_ReturnsExistingStyleError()
    {
        var screenReader = new RecordingScreenPixelReader();

        await using var provider = BuildProvider(screenReader);
        var runService = provider.GetRequiredService<IRunScriptExecutionService>();

        var result = await runService.ExecuteAsync(new CliRunExecutionRequest
        {
            Steps = ["pixelcolor 1"],
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(result.Errors, error => error.Contains("Invalid pixelcolor syntax", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScreenReading_UnsupportedPlatformFallback_ReturnsStructuredRuntimeError()
    {
        var services = new ServiceCollection();
        _ = services.AddCrossMacroCliRuntimeServices(new MinimalPlatformServiceRegistrar(), CliRuntimeProfile.OneShot);
        _ = services.AddCliServices();

        await using var provider = services.BuildServiceProvider();
        var screenReader = provider.GetRequiredService<IScreenPixelReader>();
        var pixelResult = await screenReader.GetPixelAsync(new ScreenPoint(1, 2), ScreenReadOptions.Default);
        var runService = provider.GetRequiredService<IRunScriptExecutionService>();

        var runResult = await runService.ExecuteAsync(new CliRunExecutionRequest
        {
            Steps = ["pixelcolor 1 2 sampled"],
        }, CancellationToken.None);

        Assert.False(screenReader.IsSupported);
        Assert.False(pixelResult.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Unsupported, pixelResult.ErrorKind);
        Assert.False(runResult.Success);
        Assert.Equal(CliExitCode.RuntimeError, runResult.ExitCode);
        Assert.Contains(runResult.Errors, error => error.Contains("Unsupported", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_MousePosition_UsesCliRuntimeProviderAndReturnsVariables()
    {
        await using var provider = BuildProvider(new RecordingScreenPixelReader());
        var runService = provider.GetRequiredService<IRunScriptExecutionService>();

        var result = await runService.ExecuteAsync(new CliRunExecutionRequest
        {
            Steps = ["mouse position mouse_x mouse_y"],
        }, CancellationToken.None);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        var data = Assert.IsType<RunScriptExecutionData>(result.Data);
        Assert.Equal("100", data.RuntimeVariables["mouse_x"]);
        Assert.Equal("100", data.RuntimeVariables["mouse_y"]);
    }

    private static ServiceProvider BuildProvider(RecordingScreenPixelReader screenReader)
    {
        var services = new ServiceCollection();
        _ = services.AddCrossMacroCliRuntimeServices(new ScreenReadingPlatformServiceRegistrar(screenReader), CliRuntimeProfile.OneShot);
        _ = services.AddCliServices();
        return services.BuildServiceProvider();
    }

    private sealed class ScreenReadingPlatformServiceRegistrar(ScreenReadingCliRuntimeTests.RecordingScreenPixelReader screenReader) : MinimalPlatformServiceRegistrar
    {
        private readonly RecordingScreenPixelReader _screenReader = screenReader;

        public override void RegisterPlatformServices(IServiceCollection services)
        {
            base.RegisterPlatformServices(services);
            _ = services.AddSingleton<IScreenPixelReader>(_screenReader);
        }
    }

    private class MinimalPlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public virtual void RegisterPlatformServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IRuntimeContext, TestRuntimeContext>();
            _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            _ = services.AddSingleton<IEnvironmentInfoProvider, TestEnvironmentInfoProvider>();
            _ = services.AddSingleton<ICoordinateStrategyFactory, TestCoordinateStrategyFactory>();
            _ = services.AddSingleton<IKeyboardLayoutService, TestKeyboardLayoutService>();
            _ = services.AddSingleton<IMousePositionProvider, TestMousePositionProvider>();
        }
    }

    private sealed class TestRuntimeContext : IRuntimeContext
    {
        public bool IsLinux => false;
        public bool IsWindows => true;
        public bool IsMacOS => false;
        public bool IsFlatpak => false;
        public string? SessionType => null;
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

    private sealed class RecordingScreenPixelReader : IScreenPixelReader
    {
        public string ProviderName => "recording-screen-reader";

        public bool IsSupported => true;

        public ScreenPixelSearchMatch SearchMatch { get; init; } = new(new ScreenPoint(0, 0), new ScreenPixelColor(0x00, 0x00, 0x00));

        public List<ScreenPoint> GetPixelPoints { get; } = [];

        public List<(ScreenPoint Point, ScreenPixelColor Expected, ScreenReadOptions Options)> WaitCalls { get; } = [];

        public List<(ScreenRect Region, ScreenPixelColor Expected, int Tolerance, ScreenReadOptions Options)> SearchCalls { get; } = [];

        public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            GetPixelPoints.Add(point);
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(new ScreenPixelColor(0x12, 0x34, 0x56)));
        }

        public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(
            ScreenPoint point,
            ScreenPixelColor expected,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            WaitCalls.Add((point, expected, options));
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(expected));
        }

        public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
            ScreenRect region,
            ScreenPixelColor expected,
            int tolerance,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            SearchCalls.Add((region, expected, tolerance, options));
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelSearchMatch>(SearchMatch));
        }

        public void Dispose()
        {
        }
    }
}
