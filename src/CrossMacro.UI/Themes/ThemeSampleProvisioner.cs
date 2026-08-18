using System.Reflection;

namespace CrossMacro.UI.Themes;

/// <summary>
/// Drops the user-facing README and a copy-ready template into the external themes
/// directory on first use, so the feature is discoverable without reading the docs.
/// Existing files are never overwritten; failures are logged and never fatal.
/// </summary>
internal sealed class ThemeSampleProvisioner : IThemeSampleProvisioner
{
    private const string ThemeFolderMarker = ".Themes.";
    private const string TemplateResourceSuffix = ".ExternalTheme.template.json";
    private const string ReadmeResourceSuffix = ".UserThemes.README.md";

    internal const string TemplateFileName = "_template.json";
    internal const string ReadmeFileName = "README.md";

    public void EnsureProvisioned(string themeDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeDirectoryPath);

        try
        {
            var assembly = typeof(ThemeSampleProvisioner).Assembly;
            WriteIfMissing(assembly, themeDirectoryPath, TemplateResourceSuffix, TemplateFileName);
            WriteIfMissing(assembly, themeDirectoryPath, ReadmeResourceSuffix, ReadmeFileName);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning("[Themes] Failed to provision theme samples into '{Directory}': {Error}", themeDirectoryPath, ex.Message);
        }
    }

    private static void WriteIfMissing(Assembly assembly, string directoryPath, string resourceSuffix, string fileName)
    {
        var targetPath = Path.Combine(directoryPath, fileName);
        if (File.Exists(targetPath))
        {
            return;
        }

        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.Contains(ThemeFolderMarker, StringComparison.Ordinal)
                && name.EndsWith(resourceSuffix, StringComparison.Ordinal));
        if (resourceName is null)
        {
            Log.Warning("[Themes] Embedded sample resource '{Suffix}' was not found.", resourceSuffix);
            return;
        }

        using var source = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException($"Embedded resource '{resourceName}' is missing.");
        using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.CopyTo(target);
    }
}
