using CrossMacro.Cli;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.Cli.Services;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;

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

        var result = await runService.ExecuteAsync(new RunExecutionRequest
        {
            Steps = ["pixelcolor 500 300 mycolor"]
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

        var result = await runService.ExecuteAsync(new RunExecutionRequest
        {
            Steps = ["waitcolor 500 300 00FF00 5000"]
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
            SearchMatch = new ScreenPixelSearchMatch(new ScreenPoint(3, 4), new ScreenPixelColor(0x10, 0x20, 0x30))
        };

        await using var provider = BuildProvider(screenReader);
        var runService = provider.GetRequiredService<IRunScriptExecutionService>();

        var result = await runService.ExecuteAsync(new RunExecutionRequest
        {
            Steps = ["pixelsearch 0 0 1920 1080 FF0000 found_x found_y"]
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
            SearchMatch = new ScreenPixelSearchMatch(new ScreenPoint(3, 4), new ScreenPixelColor(0x10, 0x20, 0x30))
        };

        await using var provider = BuildProvider(screenReader);
        var runService = provider.GetRequiredService<IRunScriptExecutionService>();

        var result = await runService.ExecuteAsync(new RunExecutionRequest
        {
            Steps = ["pixelsearch 0 0 1920 1080 FF0000 found_x found_y tolerance 26"]
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

        var result = await runService.ExecuteAsync(new RunExecutionRequest
        {
            Steps = ["pixelcolor 1"]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(result.Errors, error => error.Contains("Invalid pixelcolor syntax", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScreenReading_UnsupportedPlatformFallback_ReturnsStructuredRuntimeError()
    {
        var services = new ServiceCollection();
        services.AddCrossMacroCliRuntimeServices(new MinimalPlatformServiceRegistrar(), CliRuntimeProfile.OneShot);
        services.AddCliServices();

        await using var provider = services.BuildServiceProvider();
        var screenReader = provider.GetRequiredService<IScreenPixelReader>();
        var pixelResult = await screenReader.GetPixelAsync(new ScreenPoint(1, 2), ScreenReadOptions.Default);
        var runService = provider.GetRequiredService<IRunScriptExecutionService>();

        var runResult = await runService.ExecuteAsync(new RunExecutionRequest
        {
            Steps = ["pixelcolor 1 2 sampled"]
        }, CancellationToken.None);

        Assert.False(screenReader.IsSupported);
        Assert.False(pixelResult.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Unsupported, pixelResult.ErrorKind);
        Assert.False(runResult.Success);
        Assert.Equal(CliExitCode.RuntimeError, runResult.ExitCode);
        Assert.Contains(runResult.Errors, error => error.Contains("Unsupported", StringComparison.Ordinal));
    }

    private static ServiceProvider BuildProvider(RecordingScreenPixelReader screenReader)
    {
        var services = new ServiceCollection();
        services.AddCrossMacroCliRuntimeServices(new ScreenReadingPlatformServiceRegistrar(screenReader), CliRuntimeProfile.OneShot);
        services.AddCliServices();
        return services.BuildServiceProvider();
    }

    private sealed class ScreenReadingPlatformServiceRegistrar : MinimalPlatformServiceRegistrar
    {
        private readonly RecordingScreenPixelReader _screenReader;

        public ScreenReadingPlatformServiceRegistrar(RecordingScreenPixelReader screenReader)
        {
            _screenReader = screenReader;
        }

        public override void RegisterPlatformServices(IServiceCollection services)
        {
            base.RegisterPlatformServices(services);
            services.AddSingleton<IScreenPixelReader>(_screenReader);
        }
    }

    private class MinimalPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public PlatformClipboardRegistration ClipboardRegistration => PlatformClipboardRegistration.Default;

        public virtual void RegisterPlatformServices(IServiceCollection services)
        {
            services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            services.AddSingleton<IEnvironmentInfoProvider, TestEnvironmentInfoProvider>();
            services.AddSingleton<ICoordinateStrategyFactory, TestCoordinateStrategyFactory>();
            services.AddSingleton<IKeyboardLayoutService, TestKeyboardLayoutService>();
            services.AddSingleton<IMousePositionProvider, TestMousePositionProvider>();
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
            return Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(new ScreenPixelColor(0x12, 0x34, 0x56)));
        }

        public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(
            ScreenPoint point,
            ScreenPixelColor expected,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            WaitCalls.Add((point, expected, options));
            return Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(expected));
        }

        public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
            ScreenRect region,
            ScreenPixelColor expected,
            int tolerance,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            SearchCalls.Add((region, expected, tolerance, options));
            return Task.FromResult(ScreenReadResult<ScreenPixelSearchMatch>.Success(SearchMatch));
        }

        public void Dispose()
        {
        }
    }
}
