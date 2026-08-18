namespace CrossMacro.UI.Themes;

public sealed class ThemeService : IThemeService
{
    private readonly IResourceDictionary? _resourceRoot;
    private readonly IExternalThemeSource _externalThemeSource;
    private readonly Dictionary<string, IResourceDictionary> _themeCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ThemeDescriptor> _themesByName = CreateThemeIndex(ThemeCatalog.Themes);

    internal ThemeService(IResourceDictionary? resourceRoot, IExternalThemeSource externalThemeSource)
    {
        _resourceRoot = resourceRoot;
        _externalThemeSource = externalThemeSource ?? throw new ArgumentNullException(nameof(externalThemeSource));
        _ = RefreshCatalog(new List<string>());
    }

    public IReadOnlyList<string> AvailableThemes { get; private set; } = ThemeCatalog.ThemeNames;

    public string CurrentTheme { get; private set; } = ThemeCatalog.DefaultThemeName;

    public bool TryApplyTheme(string themeName, out string themeError)
    {
        // Theme brushes are Avalonia media objects with thread affinity; they must be
        // created and merged on the UI thread. Startup reaches here through
        // ConfigureAwait(false) continuations, so marshal onto the dispatcher.
        string error = string.Empty;
        var result = RunOnUiThread(() =>
        {
            if (!TryGetResourceRoot(out var resourceRoot))
            {
                error = "Application resources are not available.";
                CurrentTheme = ThemeCatalog.DefaultThemeName;
                return false;
            }

            return TryApplyThemeCore(resourceRoot, themeName, out error);
        });

        themeError = error;
        return result;
    }

    public bool TryRefreshThemes(out string themeError)
    {
        string error = string.Empty;
        var result = RunOnUiThread(() => TryRefreshThemesCore(out error));
        themeError = error;
        return result;
    }

    private bool TryRefreshThemesCore(out string themeError)
    {
        var diagnostics = new List<string>();
        var success = RefreshCatalog(diagnostics);

        if (!_themesByName.ContainsKey(CurrentTheme))
        {
            diagnostics.Add($"Current theme '{CurrentTheme}' is no longer available. Fallback to {ThemeCatalog.DefaultThemeName} applied.");
            CurrentTheme = ThemeCatalog.DefaultThemeName;
            success = false;
        }

        if (TryGetResourceRoot(out var resourceRoot) && !TryApplyThemeCore(resourceRoot, CurrentTheme, out var applyError))
        {
            diagnostics.Add(applyError);
            success = false;
        }

        themeError = JoinDiagnostics(diagnostics);
        return success;
    }

    private static T RunOnUiThread<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
        // No Avalonia app (unit tests) → no pumped UI thread → run inline; a blocking Invoke would deadlock.
        return dispatcher.CheckAccess() || Avalonia.Application.Current is null
            ? action()
            : dispatcher.Invoke(action);
    }

    private bool RefreshCatalog(List<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        ExternalThemeLoadResult externalThemes;
        var sourceFailed = false;
        try
        {
            externalThemes = _externalThemeSource.LoadThemes();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A misbehaving theme source must not take down DI resolution or theme refresh.
            Log.Warning(ex, "[Themes] External theme source failed");
            diagnostics.Add($"External themes could not be loaded: {ex.Message}");
            externalThemes = ExternalThemeLoadResult.Empty;
            sourceFailed = true;
        }

        var discoveredThemes = new List<ThemeDescriptor>(ThemeCatalog.Themes);
        var knownNames = new HashSet<string>(ThemeCatalog.ThemeNames, StringComparer.OrdinalIgnoreCase);
        var success = externalThemes.Diagnostics.Count is 0 && !sourceFailed;

        diagnostics.AddRange(externalThemes.Diagnostics);
        foreach (var theme in externalThemes.Themes.OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!knownNames.Add(theme.Name))
            {
                var duplicateMessage = $"Skipped theme '{theme.Name}' because that name is already used by another theme.";
                diagnostics.Add(duplicateMessage);
                Log.Warning("[Themes] {Diagnostic}", duplicateMessage);
                success = false;
                continue;
            }

            discoveredThemes.Add(theme);
        }

        AvailableThemes = discoveredThemes.Select(theme => theme.Name).ToArray();
        _themesByName = CreateThemeIndex(discoveredThemes);
        _themeCache.Clear();
        return success;
    }

    private bool TryApplyThemeCore(
        IResourceDictionary resourceRoot,
        string themeName,
        out string themeError)
    {
        var requestedThemeWasValid = TryResolveTheme(themeName, out var requestedTheme);
        var appliedTheme = requestedTheme;

        if (!TryCreateThemeDictionary(requestedTheme, out var targetThemeDictionary, out var loadError))
        {
            appliedTheme = ThemeCatalog.DefaultTheme;
            if (!TryCreateThemeDictionary(appliedTheme, out targetThemeDictionary, out var fallbackError))
            {
                CurrentTheme = ThemeCatalog.DefaultThemeName;
                themeError = JoinDiagnostics([loadError, fallbackError]);
                return false;
            }

            if (targetThemeDictionary is null)
            {
                CurrentTheme = ThemeCatalog.DefaultThemeName;
                themeError = $"Theme '{appliedTheme.Name}' resolved to a null resource dictionary.";
                return false;
            }

            ThemeResourceDictionaryFactory.ReplaceActiveTheme(resourceRoot, targetThemeDictionary);
            CurrentTheme = appliedTheme.Name;
            themeError = requestedThemeWasValid
                ? $"Theme '{requestedTheme.Name}' could not be loaded. Fallback to {ThemeCatalog.DefaultThemeName} applied. {loadError}"
                : $"Unknown theme '{themeName}'. Fallback to {ThemeCatalog.DefaultThemeName} applied.";
            return false;
        }

        if (targetThemeDictionary is null)
        {
            CurrentTheme = ThemeCatalog.DefaultThemeName;
            themeError = $"Theme '{appliedTheme.Name}' resolved to a null resource dictionary.";
            return false;
        }

        ThemeResourceDictionaryFactory.ReplaceActiveTheme(resourceRoot, targetThemeDictionary);
        CurrentTheme = appliedTheme.Name;

        if (!requestedThemeWasValid)
        {
            themeError = $"Unknown theme '{themeName}'. Fallback to {ThemeCatalog.DefaultThemeName} applied.";
            return false;
        }

        themeError = string.Empty;
        return true;
    }

    private bool TryResolveTheme(string? name, out ThemeDescriptor descriptor)
    {
        if (!string.IsNullOrWhiteSpace(name) && _themesByName.TryGetValue(name, out descriptor!))
        {
            return true;
        }

        descriptor = ThemeCatalog.DefaultTheme;
        return false;
    }

    private bool TryCreateThemeDictionary(
        ThemeDescriptor descriptor,
        out IResourceDictionary? dictionary,
        out string themeError)
    {
        if (_themeCache.TryGetValue(descriptor.Name, out dictionary))
        {
            themeError = string.Empty;
            return true;
        }

        try
        {
            dictionary = ThemeResourceDictionaryFactory.Create(descriptor);
            _themeCache[descriptor.Name] = dictionary;
            themeError = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            dictionary = null;
            var sourceLabel = descriptor.SourceKind is ThemeSourceKind.ExternalFile && !string.IsNullOrWhiteSpace(descriptor.SourcePath)
                ? descriptor.SourcePath
                : descriptor.Name;
            themeError = $"Theme definition '{sourceLabel}' is invalid: {ex.Message}";
            Log.Warning(ex, "[Themes] Failed to build theme '{Theme}' from {Source}", descriptor.Name, sourceLabel);
            return false;
        }
    }

    private bool TryGetResourceRoot([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IResourceDictionary? resourceRoot)
    {
        var candidate = _resourceRoot ?? Avalonia.Application.Current?.Resources;
        if (candidate is null)
        {
            resourceRoot = null;
            return false;
        }

        resourceRoot = candidate;
        return true;
    }

    private static Dictionary<string, ThemeDescriptor> CreateThemeIndex(IEnumerable<ThemeDescriptor> themes)
    {
        return themes.ToDictionary(theme => theme.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static string JoinDiagnostics(IEnumerable<string> diagnostics)
    {
        return string.Join(' ', diagnostics.Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic)));
    }
}
