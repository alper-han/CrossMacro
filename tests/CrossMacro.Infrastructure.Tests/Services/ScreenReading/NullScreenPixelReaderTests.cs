namespace CrossMacro.Infrastructure.Tests.Services.ScreenReading;

public sealed class NullScreenPixelReaderTests
{
    [Fact]
    public async Task WhenCancellationIsAlreadyRequested_ThrowsBeforeReportingUnsupported()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var options = new ScreenReadOptions(cancellationToken: cancellation.Token);
        var reader = NullScreenPixelReader.Instance;

        var operations = new Func<Task>[]
        {
            () => reader.GetPixelAsync(new ScreenPoint(1, 2), options),
            () => reader.WaitForPixelAsync(new ScreenPoint(1, 2), new ScreenPixelColor(1, 2, 3), options),
            () => reader.SearchPixelAsync(new ScreenRect(0, 0, 1, 1), new ScreenPixelColor(1, 2, 3), 0, options),
            () => reader.SearchImageAsync(region: null, imageTemplate: null!, options: null!, readOptions: options),
        };

        foreach (var operation in operations)
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(operation);
        }
    }
}
