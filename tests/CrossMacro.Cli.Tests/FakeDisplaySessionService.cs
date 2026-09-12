namespace CrossMacro.Cli.Tests;

internal sealed class FakeDisplaySessionService(bool supported, string reason) : IDisplaySessionService
{
    private readonly bool _supported = supported;
    private readonly string _reason = reason;

    public bool IsSessionSupported(out string reason)
    {
        reason = _reason;
        return _supported;
    }

    public ValueTask<(bool Supported, string Reason)> IsSessionSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult((_supported, _reason));
    }
}
