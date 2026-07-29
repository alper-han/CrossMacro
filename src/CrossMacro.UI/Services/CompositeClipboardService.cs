
namespace CrossMacro.UI.Services;

public class CompositeClipboardService(
    IHostClipboardService flatpakHostService,
    ILinuxClipboardService linuxService,
    AvaloniaClipboardService avaloniaService,
    IRuntimeContext runtimeContext) : IClipboardService
{
    private readonly IHostClipboardService _flatpakHostService = flatpakHostService;
    private readonly ILinuxClipboardService _linuxService = linuxService;
    private readonly AvaloniaClipboardService _avaloniaService = avaloniaService;
    private readonly IRuntimeContext _runtimeContext = runtimeContext;
    private bool _linuxInitialized;
    private bool _flatpakHostInitialized;
    private bool _preferAvaloniaOnNativeX11;

    public bool IsSupported =>
        (_runtimeContext.IsFlatpak && (!_flatpakHostInitialized || _flatpakHostService.IsSupported)) ||
        !_linuxInitialized ||
        _linuxService.IsSupported ||
        _avaloniaService.IsSupported;

    private async Task InitializeLinuxAsync(CancellationToken cancellationToken)
    {
        if (_linuxInitialized)
        {
            return;
        }

        await _linuxService.InitializeAsync(cancellationToken).ConfigureAwait(false);
        _linuxInitialized = true;
    }

    private async Task InitializeFlatpakHostAsync(CancellationToken cancellationToken)
    {
        if (_flatpakHostInitialized)
        {
            return;
        }

        await _flatpakHostService.InitializeAsync(cancellationToken).ConfigureAwait(false);
        _flatpakHostInitialized = true;
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_runtimeContext.IsFlatpak && await TrySetFlatpakHostAsync(text, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (ShouldPreferAvaloniaOnNativeX11() && await TrySetAvaloniaAsync(text, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await InitializeLinuxAsync(cancellationToken).ConfigureAwait(false);

        if (_linuxService.IsSupported)
        {
            await _linuxService.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
            return;
        }

        await SetAvaloniaFallbackAsync(text, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        if (_runtimeContext.IsFlatpak)
        {
            var flatpakHostResult = await TryGetFlatpakHostAsync(cancellationToken).ConfigureAwait(false);
            if (flatpakHostResult.Handled)
            {
                return flatpakHostResult.Text;
            }
        }

        if (ShouldPreferAvaloniaOnNativeX11())
        {
            var avaloniaResult = await TryGetAvaloniaAsync(cancellationToken).ConfigureAwait(false);
            if (avaloniaResult.Handled)
            {
                return avaloniaResult.Text;
            }
        }

        await InitializeLinuxAsync(cancellationToken).ConfigureAwait(false);

        if (_linuxService.IsSupported)
        {
            return await _linuxService.GetTextAsync(cancellationToken).ConfigureAwait(false);
        }

        return await GetAvaloniaFallbackAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldPreferAvaloniaOnNativeX11()
    {
        if (_runtimeContext.IsFlatpak || !string.Equals(_runtimeContext.SessionType, "x11", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _preferAvaloniaOnNativeX11 || !_linuxInitialized;
    }

    private async Task<bool> TrySetAvaloniaAsync(string text, CancellationToken cancellationToken)
    {
        if (!_avaloniaService.IsSupported)
        {
            _preferAvaloniaOnNativeX11 = false;
            return false;
        }

        try
        {
            Log.Debug("[CompositeClipboard] Native X11 detected, using Avalonia clipboard before shell fallbacks");
            await _avaloniaService.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
            _preferAvaloniaOnNativeX11 = true;
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _preferAvaloniaOnNativeX11 = false;
            Log.Warning(ex, "[CompositeClipboard] Avalonia clipboard failed on native X11; falling back to shell clipboard");
            return false;
        }
    }

    private async Task<(bool Handled, string? Text)> TryGetAvaloniaAsync(CancellationToken cancellationToken)
    {
        if (!_avaloniaService.IsSupported)
        {
            _preferAvaloniaOnNativeX11 = false;
            return (false, null);
        }

        try
        {
            Log.Debug("[CompositeClipboard] Native X11 detected, reading Avalonia clipboard before shell fallbacks");
            var text = await _avaloniaService.GetTextAsync(cancellationToken).ConfigureAwait(false);
            _preferAvaloniaOnNativeX11 = true;
            return (true, text);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _preferAvaloniaOnNativeX11 = false;
            Log.Warning(ex, "[CompositeClipboard] Avalonia clipboard read failed on native X11; falling back to shell clipboard");
            return (false, null);
        }
    }

    private async Task SetAvaloniaFallbackAsync(string text, CancellationToken cancellationToken)
    {
        Log.Debug("[CompositeClipboard] Linux shell clipboard tools not found, falling back to Avalonia clipboard");
        await _avaloniaService.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> GetAvaloniaFallbackAsync(CancellationToken cancellationToken)
    {
        Log.Debug("[CompositeClipboard] Linux shell clipboard tools not found, falling back to Avalonia clipboard");
        return await _avaloniaService.GetTextAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TrySetFlatpakHostAsync(string text, CancellationToken cancellationToken)
    {
        await InitializeFlatpakHostAsync(cancellationToken).ConfigureAwait(false);

        if (!_flatpakHostService.IsSupported)
        {
            Log.Debug("[CompositeClipboard] Flatpak host clipboard unavailable; trying sandbox clipboard fallbacks");
            return false;
        }

        try
        {
            Log.Debug("[CompositeClipboard] Flatpak detected, using host clipboard via flatpak-spawn");
            await _flatpakHostService.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[CompositeClipboard] Flatpak host clipboard failed; falling back to sandbox clipboard");
            return false;
        }
    }

    private async Task<(bool Handled, string? Text)> TryGetFlatpakHostAsync(CancellationToken cancellationToken)
    {
        await InitializeFlatpakHostAsync(cancellationToken).ConfigureAwait(false);

        if (!_flatpakHostService.IsSupported)
        {
            Log.Debug("[CompositeClipboard] Flatpak host clipboard unavailable; trying sandbox clipboard fallbacks");
            return (false, null);
        }

        try
        {
            Log.Debug("[CompositeClipboard] Flatpak detected, reading host clipboard via flatpak-spawn");
            return (true, await _flatpakHostService.GetTextAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[CompositeClipboard] Flatpak host clipboard read failed; falling back to sandbox clipboard");
            return (false, null);
        }
    }
}
