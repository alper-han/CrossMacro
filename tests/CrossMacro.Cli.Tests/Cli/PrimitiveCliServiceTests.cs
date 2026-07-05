using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Cli.Tests;

public sealed class PrimitiveCliServiceTests
{
    [Fact]
    public async Task Clipboard_Get_ReturnsClipboardTextData()
    {
        var clipboard = new FakeClipboardService { Text = "hello" };
        var service = new ClipboardCliService(clipboard);

        var result = await service.GetAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("hello", Assert.IsType<ClipboardTextData>(result.Data).Value);
    }

    [Fact]
    public async Task Clipboard_SetFile_ReadsFileAndSetsText()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "from file");
        try
        {
            var clipboard = new FakeClipboardService();
            var service = new ClipboardCliService(clipboard);

            var result = await service.SetFileAsync(path, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("from file", clipboard.Text);
            Assert.Equal(9, Assert.IsType<ClipboardSetData>(result.Data).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Clipboard_Clear_SetsEmptyClipboardText()
    {
        var clipboard = new FakeClipboardService { Text = "hello" };
        var service = new ClipboardCliService(clipboard);

        var result = await service.ClearAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, clipboard.Text);
        var data = Assert.IsType<ClipboardSetData>(result.Data);
        Assert.Equal(0, data.Length);
        Assert.Equal("clear", data.Source);
    }

    [Fact]
    public async Task Clipboard_Unsupported_ReturnsEnvironmentError()
    {
        var service = new ClipboardCliService(new FakeClipboardService { IsSupported = false });

        var result = await service.GetAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("not supported", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Window_SearchAndFocus_UseSubstringSelectorsAndMutator()
    {
        var manager = new FakeWindowManager
        {
            Windows =
            [
                new WindowInfo { Address = "0x1", Title = "Docs - Firefox", Class = "firefox" },
                new WindowInfo { Address = "0x2", Title = "Editor", Class = "Code" }
            ]
        };
        var service = new WindowCliService(manager);

        var search = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.Search, new WindowSelector(WindowSelectorKind.Title, "fire")), CancellationToken.None);
        var focus = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.Focus, new WindowSelector(WindowSelectorKind.Class, "code")), CancellationToken.None);

        Assert.True(search.Success);
        var data = Assert.IsType<WindowListData>(search.Data);
        Assert.Equal("0x1", Assert.Single(data.Windows).Address);
        Assert.True(focus.Success);
        Assert.Equal("code", manager.FocusedClass);
    }

    [Fact]
    public async Task Window_NullManager_ReturnsUnsupportedEnvironmentError()
    {
        var manager = new NullWindowManager();
        var service = new WindowCliService(manager);

        var result = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.List), CancellationToken.None);

        Assert.False(manager.IsSupported);
        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task Window_InvalidActionOptions_ReturnInvalidArguments()
    {
        var service = new WindowCliService(new FakeWindowManager());

        var missingSelector = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.Search), CancellationToken.None);
        var missingCoordinates = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.Move, X: 1), CancellationToken.None);
        var unsupportedCloseSelector = await service.ExecuteAsync(new WindowCliOptions(WindowCliAction.Close, new WindowSelector(WindowSelectorKind.Class, "code")), CancellationToken.None);

        AssertInvalidArguments(missingSelector);
        AssertInvalidArguments(missingCoordinates);
        AssertInvalidArguments(unsupportedCloseSelector);
    }

    [Fact]
    public async Task Screen_PixelRelative_UsesMousePositionProvider()
    {
        var reader = new FakeScreenPixelReader();
        var service = new ScreenCliService(reader, new FakeMousePositionProvider { Position = (100, 200) });

        var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.Pixel, 5, -10, Relative: true), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new ScreenPoint(105, 190), reader.LastPoint);
        var data = Assert.IsType<ScreenPixelData>(result.Data);
        Assert.Equal("123456", data.Color);
        Assert.True(data.Relative);
    }

    [Fact]
    public async Task Screen_WaitAndSearch_ForwardOptionsToReader()
    {
        var reader = new FakeScreenPixelReader();
        var service = new ScreenCliService(reader, new FakeMousePositionProvider());

        var wait = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.WaitColor, 1, 2, new ScreenPixelColor(0, 255, 0), TimeoutMs: 250), CancellationToken.None);
        var search = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchColor, 0, 0, new ScreenPixelColor(255, 0, 0), X2: 10, Y2: 20, Tolerance: 26), CancellationToken.None);

        Assert.True(wait.Success);
        Assert.Equal(TimeSpan.FromMilliseconds(250), reader.LastWaitOptions.Timeout);
        Assert.True(search.Success);
        Assert.Equal(new ScreenRect(0, 0, 10, 20), reader.LastRegion);
        Assert.Equal(26, reader.LastTolerance);
    }

    [Fact]
    public async Task Screen_UnsupportedReader_ReturnsEnvironmentError()
    {
        var service = new ScreenCliService(new FakeScreenPixelReader { IsSupported = false }, new FakeMousePositionProvider());

        var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.Pixel, 1, 2), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task Screen_InvalidActionOptions_ReturnInvalidArguments()
    {
        var service = new ScreenCliService(new FakeScreenPixelReader(), new FakeMousePositionProvider());

        var missingWaitColor = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.WaitColor, 1, 2), CancellationToken.None);
        var missingSearchBounds = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchColor, 0, 0, new ScreenPixelColor(255, 0, 0)), CancellationToken.None);
        var zeroWidthSearch = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchColor, 0, 0, new ScreenPixelColor(255, 0, 0), X2: 0, Y2: 10), CancellationToken.None);

        AssertInvalidArguments(missingWaitColor);
        AssertInvalidArguments(missingSearchBounds);
        AssertInvalidArguments(zeroWidthSearch);
    }

    private static void AssertInvalidArguments(CliCommandExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool IsSupported { get; init; } = true;
        public string? Text { get; set; }
        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Text = text;
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult(Text);
    }

    private sealed class FakeWindowManager : IWindowManager
    {
        public IReadOnlyList<WindowInfo> Windows { get; init; } = [];
        public string? FocusedClass { get; private set; }
        public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(Windows.FirstOrDefault());
        public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Windows);
        public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
        {
            FocusedClass = classSubstring;
            return Task.FromResult(true);
        }

        public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("1");
        public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeScreenPixelReader : IScreenPixelReader
    {
        public string ProviderName => "fake-screen";
        public bool IsSupported { get; init; } = true;
        public ScreenPoint LastPoint { get; private set; }
        public ScreenRect LastRegion { get; private set; }
        public int LastTolerance { get; private set; }
        public ScreenReadOptions LastWaitOptions { get; private set; }
        public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
        {
            LastPoint = point;
            return Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(new ScreenPixelColor(0x12, 0x34, 0x56)));
        }

        public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(ScreenPoint point, ScreenPixelColor expected, ScreenReadOptions options)
        {
            LastWaitOptions = options;
            return Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(expected));
        }

        public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(ScreenRect region, ScreenPixelColor expected, int tolerance, ScreenReadOptions options)
        {
            LastRegion = region;
            LastTolerance = tolerance;
            return Task.FromResult(ScreenReadResult<ScreenPixelSearchMatch>.Success(new ScreenPixelSearchMatch(new ScreenPoint(region.X + 1, region.Y + 1), expected)));
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeMousePositionProvider : IMousePositionProvider
    {
        public string ProviderName => "fake-mouse";
        public bool IsSupported => true;
        public (int X, int Y)? Position { get; init; } = (0, 0);
        public Task<(int X, int Y)?> GetAbsolutePositionAsync() => Task.FromResult(Position);
        public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>((1920, 1080));
        public void Dispose()
        {
        }
    }
}
