namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class CountingDisposable : IDisposable
{
    public int DisposeCount { get; private set; }

    public void Dispose() => DisposeCount++;
}
