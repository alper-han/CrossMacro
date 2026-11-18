
namespace CrossMacro.Cli.Tests;

public sealed class RunScriptExecutionServiceTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task ExecuteAsync_WhenImageAssetExceedsEncodedLimit_RejectsBeforeReadingContents()
    {
        var path = _directory.Combine("oversized.png");
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(ScreenImageAssetPolicy.MaxEncodedBytes + 1L);
        }

        var runtimeService = Substitute.For<CrossMacro.Application.Runtime.IRunExecutionService>();
        var service = new RunScriptExecutionService(runtimeService, new ImageAssetCodec());
        var result = await service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["imagesearch button"],
            ImageAssets = [new RunImageAssetRequest("button", path)],
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.ExitCode);
        Assert.Contains(result.Errors, error => error.Contains("maximum encoded size", StringComparison.OrdinalIgnoreCase));
        _ = runtimeService.DidNotReceive().ExecuteAsync(Arg.Any<CrossMacro.Application.Runtime.RunExecutionRequest>(), Arg.Any<CancellationToken>());
    }
}
