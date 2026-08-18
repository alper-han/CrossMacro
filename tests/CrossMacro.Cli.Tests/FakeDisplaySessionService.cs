namespace CrossMacro.Cli.Tests;

internal sealed class FakeDisplaySessionService : IDisplaySessionService
{
    private readonly bool _supported;
    private readonly string _reason;

    public FakeDisplaySessionService(bool supported, string reason)
    {
        _supported = supported;
        _reason = reason;
    }

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
