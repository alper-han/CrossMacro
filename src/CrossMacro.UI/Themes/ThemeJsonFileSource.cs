namespace CrossMacro.UI.Themes;

internal sealed class ThemeJsonFileSource(
    IThemeDirectoryResolver themeDirectoryResolver,
    IThemeSampleProvisioner? sampleProvisioner = null) : IExternalThemeSource
{
    private const char DraftFilePrefix = '_';

    private readonly IThemeDirectoryResolver _themeDirectoryResolver = themeDirectoryResolver ?? throw new ArgumentNullException(nameof(themeDirectoryResolver));
    private readonly IThemeSampleProvisioner? _sampleProvisioner = sampleProvisioner;

    public ExternalThemeLoadResult LoadThemes()
    {
        var themes = new List<ThemeDescriptor>();
        var diagnostics = new List<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string themeDirectoryPath;

        try
        {
            themeDirectoryPath = _themeDirectoryResolver.GetThemeDirectoryPath();
            _ = Directory.CreateDirectory(themeDirectoryPath);
            _sampleProvisioner?.EnsureProvisioned(themeDirectoryPath);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            diagnostics.Add($"External theme directory is unavailable: {ex.Message}");
            LogDiagnostics(diagnostics);
            return new ExternalThemeLoadResult(themes, diagnostics);
        }

        string[] themeFiles;
        try
        {
            themeFiles = Directory.GetFiles(themeDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            diagnostics.Add($"Failed to enumerate external themes in '{themeDirectoryPath}': {ex.Message}");
            LogDiagnostics(diagnostics);
            return new ExternalThemeLoadResult(themes, diagnostics);
        }

        foreach (var filePath in themeFiles.Order(StringComparer.OrdinalIgnoreCase))
        {
            // Underscore-prefixed files are drafts/disabled themes (e.g. the shipped template).
            if (Path.GetFileName(filePath).StartsWith(DraftFilePrefix))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                var theme = ThemeDocumentParser.Parse(stream, ThemeSourceKind.ExternalFile, filePath);

                if (!seenNames.Add(theme.Name))
                {
                    diagnostics.Add($"Skipped duplicate external theme '{theme.Name}' from '{Path.GetFileName(filePath)}'.");
                    continue;
                }

                themes.Add(theme);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                diagnostics.Add($"Skipped theme file '{Path.GetFileName(filePath)}': {ex.Message}");
            }
        }

        LogDiagnostics(diagnostics);

        return new ExternalThemeLoadResult(themes, diagnostics);
    }

    private static void LogDiagnostics(IEnumerable<string> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Log.Warning("[Themes] {Diagnostic}", diagnostic);
        }
    }
}
