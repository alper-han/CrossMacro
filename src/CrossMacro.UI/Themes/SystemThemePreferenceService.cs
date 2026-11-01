namespace CrossMacro.UI.Themes;

internal sealed class SystemThemePreferenceService(
    IThemeService themeService,
    IUiDispatcher dispatcher) : ISystemThemePreferenceService
{
    private readonly IThemeService _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
    private readonly IUiDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    private bool _started;
    private bool _disposed;
    public event EventHandler<string>? ThemeResolved;

    public void Start(ThemePreferenceMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SetMode(mode);
    }

    public void SetMode(ThemePreferenceMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = _dispatcher.InvokeAsync(() =>
        {
            if (mode is ThemePreferenceMode.Manual && _started && Avalonia.Application.Current is { } manualApplication)
            {
                manualApplication.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
                _started = false;
            }
            else if (mode is ThemePreferenceMode.System && !_started && Avalonia.Application.Current is { } systemApplication)
            {
                systemApplication.ActualThemeVariantChanged += OnActualThemeVariantChanged;
                _started = true;
            }

            _ = _themeService.TrySetPreferenceMode(mode, out _);
            if (mode is ThemePreferenceMode.System && Avalonia.Application.Current is { } application)
            {
                ApplyVariant(application.ActualThemeVariant);
            }
        });
    }
    private void OnActualThemeVariantChanged(object? sender, EventArgs args)
    {
        if (sender is Avalonia.Application application)
        {
            _dispatcher.Post(() => ApplyVariant(application.ActualThemeVariant));
        }
    }

    private void ApplyVariant(ThemeVariant? variant)
    {
        void Apply()
        {
            if (_themeService.TryApplySystemTheme(variant, out _))
            {
                ThemeResolved?.Invoke(this, _themeService.CurrentTheme);
            }
        }
        if (_dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            _dispatcher.Post(Apply);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _ = _dispatcher.InvokeAsync(() =>
        {
            if (_started && Avalonia.Application.Current is { } application)
            {
                application.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
            }

            _started = false;
            _disposed = true;
        });
    }
}
