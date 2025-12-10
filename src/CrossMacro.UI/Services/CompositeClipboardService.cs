using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using Serilog;

namespace CrossMacro.UI.Services;

public class CompositeClipboardService : IClipboardService
{
    private readonly LinuxShellClipboardService _linuxService;
    private readonly AvaloniaClipboardService _avaloniaService;
    private bool _initialized;

    public CompositeClipboardService(
        LinuxShellClipboardService linuxService,
        AvaloniaClipboardService avaloniaService)
    {
        _linuxService = linuxService;
        _avaloniaService = avaloniaService;
    }

    public bool IsSupported => _linuxService.IsSupported || _avaloniaService.IsSupported;

    private async Task InitializeAsync()
    {
        if (_initialized) return;
        
        await _linuxService.InitializeAsync();
        _initialized = true;
    }

    public async Task SetTextAsync(string text)
    {
        await InitializeAsync();

        if (_linuxService.IsSupported)
        {
             await _linuxService.SetTextAsync(text);
             return;
        }

        Log.Debug("[CompositeClipboard] Linux shell clipboard tools not found, falling back to Avalonia clipboard");
        await _avaloniaService.SetTextAsync(text);
    }

    public async Task<string?> GetTextAsync()
    {
        await InitializeAsync();

        if (_linuxService.IsSupported)
        {
            return await _linuxService.GetTextAsync();
        }
        
         Log.Debug("[CompositeClipboard] Linux shell clipboard tools not found, falling back to Avalonia clipboard");
         return await _avaloniaService.GetTextAsync();
    }
}
