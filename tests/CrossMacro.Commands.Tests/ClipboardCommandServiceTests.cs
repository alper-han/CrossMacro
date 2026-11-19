using CrossMacro.Commands.Clipboard;
using CrossMacro.Core.Services.Clipboard;

namespace CrossMacro.Commands.Tests;

public sealed class ClipboardCommandServiceTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task Get_ReturnsClipboardTextData()
    {
        var service = new ClipboardCommandService(new FakeClipboardService { Text = "hello" });

        var result = await service.GetAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("hello", Assert.IsType<ClipboardTextData>(result.Data).Value);
    }

    [Fact]
    public async Task SetFile_ReadsTheFileAndSetsText()
    {
        var path = _directory.WriteFile("in.txt", "from file");
        var clipboard = new FakeClipboardService();

        var result = await new ClipboardCommandService(clipboard).SetFileAsync(path, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("from file", clipboard.Text);
        Assert.Equal(9, Assert.IsType<ClipboardSetData>(result.Data).Length);
    }

    [Fact]
    public async Task SetFile_WhenTheFileExceedsTheLimit_FailsWithoutChangingTheClipboard()
    {
        var path = _directory.WriteFile("big.txt", new string('x', ClipboardTextLimits.MaximumCharacters + 1));
        var clipboard = new FakeClipboardService { Text = "keep" };

        var result = await new ClipboardCommandService(clipboard).SetFileAsync(path, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        Assert.Equal("keep", clipboard.Text);
    }

    [Fact]
    public async Task SetFile_AtTheLimit_Succeeds()
    {
        var path = _directory.WriteFile("max.txt", new string('x', ClipboardTextLimits.MaximumCharacters));
        var clipboard = new FakeClipboardService();

        var result = await new ClipboardCommandService(clipboard).SetFileAsync(path, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ClipboardTextLimits.MaximumCharacters, clipboard.Text!.Length);
    }

    [Fact]
    public async Task SetText_WhenTheTextExceedsTheLimit_Fails()
    {
        var clipboard = new FakeClipboardService { Text = "keep" };

        var result = await new ClipboardCommandService(clipboard).SetTextAsync(new string('x', ClipboardTextLimits.MaximumCharacters + 1), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        Assert.Equal("keep", clipboard.Text);
    }

    [Fact]
    public async Task Clear_SetsEmptyClipboardText()
    {
        var clipboard = new FakeClipboardService { Text = "hello" };

        var result = await new ClipboardCommandService(clipboard).ClearAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(clipboard.Text);
        var data = Assert.IsType<ClipboardSetData>(result.Data);
        Assert.Equal(0, data.Length);
        Assert.Equal("clear", data.Source);
    }

    [Fact]
    public async Task Get_WhenTheClipboardIsUnsupported_ReturnsEnvironmentError()
    {
        var result = await new ClipboardCommandService(new FakeClipboardService { IsSupported = false }).GetAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
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
}
