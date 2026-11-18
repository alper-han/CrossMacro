
namespace CrossMacro.Platform.MacOS.Tests.Strategies;

/// <summary>Answers the position query only when <see cref="CompletePositionRequest"/> is called.</summary>
internal sealed class DelayedMousePositionProvider((int X, int Y) position) : IMousePositionProvider
{
    private readonly TaskCompletionSource<(int X, int Y)?> _positionRequest =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int AbsolutePositionQueries { get; private set; }

    public string ProviderName => "test";

    public bool IsSupported => true;

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        AbsolutePositionQueries++;
        return _positionRequest.Task;
    }

    public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>(null);

    public void CompletePositionRequest() => _ = _positionRequest.TrySetResult(position);

    public void Dispose() { /* Test provider has no resources. */ }
}
