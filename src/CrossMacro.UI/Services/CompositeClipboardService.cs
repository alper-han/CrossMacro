using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.UI.Services;

public class CompositeClipboardService : IClipboardService
{
    private readonly FlatpakHostClipboardService _flatpakHostService;
    private readonly LinuxShellClipboardService _linuxService;
    private readonly IClipboardService _avaloniaService;
    private readonly IRuntimeContext _runtimeContext;
    private bool _linuxInitialized;
    private bool _flatpakHostInitialized;
    private bool _preferAvaloniaOnNativeX11;

    public CompositeClipboardService(
        FlatpakHostClipboardService flatpakHostService,
        LinuxShellClipboardService linuxService,
        AvaloniaClipboardService avaloniaService,
        IRuntimeContext runtimeContext)
        : this(flatpakHostService, linuxService, (IClipboardService)avaloniaService, runtimeContext)
    {
    }

    internal CompositeClipboardService(
        FlatpakHostClipboardService flatpakHostService,
        LinuxShellClipboardService linuxService,
        IClipboardService avaloniaService,
        IRuntimeContext runtimeContext)
    {
        _flatpakHostService = flatpakHostService;
        _linuxService = linuxService;
        _avaloniaService = avaloniaService;
        _runtimeContext = runtimeContext;
    }

    public bool IsSupported =>
        (_runtimeContext.IsFlatpak && (!_flatpakHostInitialized || _flatpakHostService.IsSupported)) ||
        !_linuxInitialized ||
        _linuxService.IsSupported ||
        _avaloniaService.IsSupported;

    private async Task InitializeLinuxAsync(CancellationToken cancellationToken)
    {
        if (_linuxInitialized) return;
        
        await _linuxService.InitializeAsync(cancellationToken);
        _linuxInitialized = true;
    }

    private async Task InitializeFlatpakHostAsync(CancellationToken cancellationToken)
    {
        if (_flatpakHostInitialized) return;

        await _flatpakHostService.InitializeAsync(cancellationToken);
        _flatpakHostInitialized = true;
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_runtimeContext.IsFlatpak && await TrySetFlatpakHostAsync(text, cancellationToken))
        {
            return;
        }

        if (ShouldPreferAvaloniaOnNativeX11() && await TrySetAvaloniaAsync(text, cancellationToken))
        {
            return;
        }

        await InitializeLinuxAsync(cancellationToken);

        if (_linuxService.IsSupported)
        {
            await _linuxService.SetTextAsync(text, cancellationToken);
            return;
        }

        await SetAvaloniaFallbackAsync(text, cancellationToken);
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        if (_runtimeContext.IsFlatpak)
        {
            var flatpakHostResult = await TryGetFlatpakHostAsync(cancellationToken);
            if (flatpakHostResult.Handled)
            {
                return flatpakHostResult.Text;
            }
        }

        if (ShouldPreferAvaloniaOnNativeX11())
        {
            var avaloniaResult = await TryGetAvaloniaAsync(cancellationToken);
            if (avaloniaResult.Handled)
            {
                return avaloniaResult.Text;
            }
        }

        await InitializeLinuxAsync(cancellationToken);

        if (_linuxService.IsSupported)
        {
            return await _linuxService.GetTextAsync(cancellationToken);
        }
        
        return await GetAvaloniaFallbackAsync(cancellationToken);
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
            await _avaloniaService.SetTextAsync(text, cancellationToken);
            _preferAvaloniaOnNativeX11 = true;
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
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
            var text = await _avaloniaService.GetTextAsync(cancellationToken);
            _preferAvaloniaOnNativeX11 = true;
            return (true, text);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _preferAvaloniaOnNativeX11 = false;
            Log.Warning(ex, "[CompositeClipboard] Avalonia clipboard read failed on native X11; falling back to shell clipboard");
            return (false, null);
        }
    }

    private async Task SetAvaloniaFallbackAsync(string text, CancellationToken cancellationToken)
    {
        Log.Debug("[CompositeClipboard] Linux shell clipboard tools not found, falling back to Avalonia clipboard");
        await _avaloniaService.SetTextAsync(text, cancellationToken);
    }

    private async Task<string?> GetAvaloniaFallbackAsync(CancellationToken cancellationToken)
    {
        Log.Debug("[CompositeClipboard] Linux shell clipboard tools not found, falling back to Avalonia clipboard");
        return await _avaloniaService.GetTextAsync(cancellationToken);
    }

    private async Task<bool> TrySetFlatpakHostAsync(string text, CancellationToken cancellationToken)
    {
        await InitializeFlatpakHostAsync(cancellationToken);

        if (!_flatpakHostService.IsSupported)
        {
            Log.Debug("[CompositeClipboard] Flatpak host clipboard unavailable; trying sandbox clipboard fallbacks");
            return false;
        }

        try
        {
            Log.Debug("[CompositeClipboard] Flatpak detected, using host clipboard via flatpak-spawn");
            await _flatpakHostService.SetTextAsync(text, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[CompositeClipboard] Flatpak host clipboard failed; falling back to sandbox clipboard");
            return false;
        }
    }

    private async Task<(bool Handled, string? Text)> TryGetFlatpakHostAsync(CancellationToken cancellationToken)
    {
        await InitializeFlatpakHostAsync(cancellationToken);

        if (!_flatpakHostService.IsSupported)
        {
            Log.Debug("[CompositeClipboard] Flatpak host clipboard unavailable; trying sandbox clipboard fallbacks");
            return (false, null);
        }

        try
        {
            Log.Debug("[CompositeClipboard] Flatpak detected, reading host clipboard via flatpak-spawn");
            return (true, await _flatpakHostService.GetTextAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[CompositeClipboard] Flatpak host clipboard read failed; falling back to sandbox clipboard");
            return (false, null);
        }
    }
}
