namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal sealed class GnomeScreenReadingReadiness : IGnomeScreenReadingReadiness, IDisposable
{
    private readonly GnomePositionProvider _provider;
    public GnomeScreenReadingReadiness(GnomePositionProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _provider.ExtensionStatusUpdated += OnStatusUpdated;
    }
    public bool IsSession => _provider.IsSupported;
    public bool IsAvailable => IsSession && _provider.CurrentExtensionStatus?.Code is CrossMacro.Core.Services.Extensions.ExtensionStatusCode.Enabled;
    public Task Initialization => _provider.InitializationTask;
    public event EventHandler? Changed;
    private void OnStatusUpdated(object? sender, ExtensionStatusChangedEventArgs args) => Changed?.Invoke(this, EventArgs.Empty);
    public void Dispose() => _provider.ExtensionStatusUpdated -= OnStatusUpdated;
}
