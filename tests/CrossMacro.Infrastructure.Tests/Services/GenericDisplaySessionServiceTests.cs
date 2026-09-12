
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class GenericDisplaySessionServiceTests
{
    [Fact]
    public void IsSessionSupported_ReturnsTrueWithEmptyReason()
    {
        var service = new GenericDisplaySessionService();

        var result = service.IsSessionSupported(out var reason);

        _ = result.Should().BeTrue();
        _ = reason.Should().BeEmpty();
    }

    [Fact]
    public async Task IsSessionSupportedAsync_WhenCanceled_DoesNotInvokeSynchronousAdapter()
    {
        IDisplaySessionService service = new GenericDisplaySessionService();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await service.IsSessionSupportedAsync(cancellation.Token));
    }
}
