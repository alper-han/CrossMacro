namespace CrossMacro.Platform.Linux.DisplayServer;

internal sealed class CompositeMousePositionProvider :
    IMousePositionProvider,
    IMousePositionChangeSource,
    IExtensionStatusNotifier,
    IAsyncDisposable
{
    private static readonly TimeSpan PrimaryNotificationFreshness = TimeSpan.FromMilliseconds(16);

    private readonly IMousePositionProvider _primary;
    private readonly IMousePositionProvider _fallback;
    private readonly IMousePositionChangeSource _primaryChangeSource;
    private readonly IExtensionStatusNotifier? _extensionStatusNotifier;
    private long _lastPrimaryNotificationTimestamp;
    private int _disposed;

    public CompositeMousePositionProvider(
        IMousePositionProvider primary,
        IMousePositionProvider fallback)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _primaryChangeSource = primary as IMousePositionChangeSource
            ?? throw new ArgumentException("Primary provider must publish position changes.", nameof(primary));
        _extensionStatusNotifier = fallback as IExtensionStatusNotifier;
        _primaryChangeSource.PositionChanged += ForwardPositionChanged;
    }

    public string ProviderName => $"{_primary.ProviderName} (fallback: {_fallback.ProviderName})";
    public bool IsSupported => _primary.IsSupported || _fallback.IsSupported;
    public bool SupportsAbsolutePosition => _primary.SupportsAbsolutePosition || _fallback.SupportsAbsolutePosition;
    public Task<bool> InitializationTask => AwaitInitializationAsync();

    public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;

    public event EventHandler<ExtensionStatusChangedEventArgs>? ExtensionStatusUpdated
    {
        add
        {
            if (_extensionStatusNotifier is { } notifier)
            {
                notifier.ExtensionStatusUpdated += value;
            }
        }
        remove
        {
            if (_extensionStatusNotifier is { } notifier)
            {
                notifier.ExtensionStatusUpdated -= value;
            }
        }
    }

    public event EventHandler<ExtensionStatusMessageEventArgs>? ExtensionStatusChanged
    {
        add
        {
            if (_extensionStatusNotifier is { } notifier)
            {
                notifier.ExtensionStatusChanged += value;
            }
        }
        remove
        {
            if (_extensionStatusNotifier is { } notifier)
            {
                notifier.ExtensionStatusChanged -= value;
            }
        }
    }

    public ExtensionStatusChangedEventArgs? CurrentExtensionStatus =>
        _extensionStatusNotifier?.CurrentExtensionStatus;

    public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        var primaryPosition = _primary.IsSupported
            ? await _primary.GetAbsolutePositionAsync().ConfigureAwait(false)
            : null;
        bool primaryNotificationIsFresh = primaryPosition is not null
            && Stopwatch.GetElapsedTime(Volatile.Read(ref _lastPrimaryNotificationTimestamp))
                < PrimaryNotificationFreshness;
        if (primaryNotificationIsFresh || !_fallback.SupportsAbsolutePosition)
        {
            return primaryPosition;
        }

        var fallbackPosition = await _fallback.GetAbsolutePositionAsync().ConfigureAwait(false);
        return fallbackPosition ?? primaryPosition;
    }

    public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        var bounds = await GetDesktopBoundsAsync().ConfigureAwait(false);
        return bounds is not null ? (bounds.Value.Width, bounds.Value.Height) : null;
    }

    public async Task<ScreenRect?> GetDesktopBoundsAsync()
    {
        var bounds = await _primary.GetDesktopBoundsAsync().ConfigureAwait(false);
        return bounds ?? await _fallback.GetDesktopBoundsAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        _primaryChangeSource.PositionChanged -= ForwardPositionChanged;
        await DisposeProviderAsync(_primary).ConfigureAwait(false);
        if (!ReferenceEquals(_primary, _fallback))
        {
            await DisposeProviderAsync(_fallback).ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    private async Task<bool> AwaitInitializationAsync()
    {
        bool primaryInitialized = await _primary.InitializationTask.ConfigureAwait(false);
        if (primaryInitialized && _primary.IsSupported)
        {
            return true;
        }

        return await _fallback.InitializationTask.ConfigureAwait(false);
    }

    private static async ValueTask DisposeProviderAsync(IMousePositionProvider provider)
    {
        if (provider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            provider.Dispose();
        }
    }

    private void ForwardPositionChanged(object? sender, MousePositionChangedEventArgs e)
    {
        Volatile.Write(ref _lastPrimaryNotificationTimestamp, Stopwatch.GetTimestamp());
        PositionChanged?.Invoke(this, e);
    }
}
