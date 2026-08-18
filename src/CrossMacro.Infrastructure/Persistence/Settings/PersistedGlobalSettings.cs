using System.ComponentModel;

namespace CrossMacro.Infrastructure.Persistence.Settings;

/// <summary>
/// Infrastructure-owned representation of global-settings.json.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PersistedGlobalSettings
{
    public bool EnableTrayIcon { get; set; }

    public bool StartMinimized { get; set; }

    public bool SuppressFastLoopWarning { get; set; }

    public string LogLevel { get; set; } = "Information";

    public string Theme { get; set; } = "Mocha";

    public string Language { get; set; } = "en";

}
