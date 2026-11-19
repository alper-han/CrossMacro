using CrossMacro.Infrastructure.Services.Playback;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Infrastructure.Services.ScreenReading;

namespace CrossMacro.Commands.Tests;

/// <summary>Runs the screen command against the real image automation and PNG codec with a fake screen reader.</summary>
public sealed class ScreenImageFileCliTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly IScreenImageSearchReader _reader = Substitute.For<IScreenImageSearchReader>();
    private readonly ScreenCommandService _service;

    public ScreenImageFileCliTests()
    {
        _ = _reader.IsSupported.Returns(returnThis: true);
        _ = _reader.ProviderName.Returns("fake-screen");
        _service = new ScreenCommandService(
            _reader,
            mousePositionProvider: null,
            new ScreenImageAutomation(_reader, new ImageAssetCodec(), mousePositionProvider: null, inputSimulatorFactory: null, simulatorPool: null, Substitute.For<IImageClickMovementResolver>()));
    }

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task SearchImage_WhenTheFileIsMissing_ReturnsInvalidArguments()
    {
        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.SearchImage, ImagePath: _directory.Combine("missing.png")), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchImage_WhenTheTemplateExceedsTheSupportedDimensions_ReturnsInvalidArguments()
    {
        var path = _directory.Combine("huge.png");
        await File.WriteAllBytesAsync(path, CreateOversizedPngHeader(), TestContext.Current.CancellationToken);

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.SearchImage, ImagePath: path), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        Assert.Contains("not a supported PNG", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("maximum supported size of 7680x4320", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchImage_WhenTheTemplateIsAValidPng_SearchesTheScreenWithTheDecodedTemplate()
    {
        var path = await WriteTemplatePngAsync("button.png", width: 3, height: 2);
        _ = _reader.SearchImageAsync(Arg.Any<ScreenRect?>(), Arg.Any<ScreenFrame>(), Arg.Any<ScreenImageMatchOptions>(), Arg.Any<ScreenReadOptions>())
            .Returns(ScreenReadResultFactory.Success(new ScreenImageMatch(new ScreenPoint(7, 8), 0.95)));

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.SearchImage, ImagePath: path), CancellationToken.None);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        var data = Assert.IsType<ScreenSearchImageData>(result.Data);
        Assert.Equal((true, 7, 8), (data.Found, data.X, data.Y));
        _ = await _reader.Received(1).SearchImageAsync(
            Arg.Any<ScreenRect?>(),
            Arg.Is<ScreenFrame>(template => template.Width == 3 && template.Height == 2),
            Arg.Any<ScreenImageMatchOptions>(),
            Arg.Any<ScreenReadOptions>());
    }

    private async Task<string> WriteTemplatePngAsync(string fileName, int width, int height)
    {
        var path = _directory.Combine(fileName);
        var pixels = new byte[checked(width * height * 3)];
        Array.Fill<byte>(pixels, 0x56);
        using var frame = new ScreenFrame(new ScreenRect(0, 0, width, height), width * 3, ScreenPixelFormat.Rgb24, pixels);
        await using var output = File.Create(path);
        await ScreenFramePngEncoder.EncodeAsync(frame, output, CancellationToken.None);
        return path;
    }

    private static byte[] CreateOversizedPngHeader() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x1E, 0x01,
        0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00,
        0x6C, 0xF7, 0xBC, 0x13,
    ];
}
