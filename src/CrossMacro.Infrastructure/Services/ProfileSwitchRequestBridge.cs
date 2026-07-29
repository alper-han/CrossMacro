namespace CrossMacro.Infrastructure.Services;

internal sealed class ProfileSwitchRequestBridge : IProfileSwitchRequests
{
    private IProfileSwitchRequestHandler? _handler;

    public void SetHandler(IProfileSwitchRequestHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (Interlocked.CompareExchange(ref _handler, handler, comparand: null) is not null)
        {
            throw new InvalidOperationException("A profile switch request handler is already configured.");
        }
    }

    public Task RequestSwitchAsync(string profileId)
    {
        var handler = Volatile.Read(ref _handler)
            ?? throw new InvalidOperationException("No profile switch request handler is configured.");
        return handler.HandleSwitchRequestAsync(profileId);
    }
}
