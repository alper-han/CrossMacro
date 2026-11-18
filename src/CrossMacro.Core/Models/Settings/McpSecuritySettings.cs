namespace CrossMacro.Core.Models.Settings;

/// <summary>
/// Persisted local MCP capability switches and path roots. Read-oriented
/// capabilities are enabled by default; shell and command execution, file
/// writes and settings writes must be enabled explicitly, and file access
/// is denied until a root is configured for its path class. Values stored in
/// the settings file always win over these defaults.
/// </summary>
public sealed class McpSecuritySettings
{
    public McpPathSettings Paths { get; set; } = new();

    public bool AllowMacroRead { get; set; } = true;

    public bool AllowScreenRead { get; set; } = true;

    public bool AllowClipboardRead { get; set; } = true;

    public bool AllowClipboardWrite { get; set; } = true;

    public bool AllowInputAutomation { get; set; } = true;

    public bool AllowRecording { get; set; } = true;

    public bool AllowWindowRead { get; set; } = true;

    public bool AllowWindowControl { get; set; } = true;

    public bool AllowFileRead { get; set; } = true;

    public bool AllowFileWrite { get; set; }

    public bool AllowCommandExecute { get; set; }

    public bool AllowShellExecute { get; set; }

    public bool AllowSettingsRead { get; set; } = true;

    public bool AllowSettingsWrite { get; set; }

    public bool AllowProfileManage { get; set; } = true;

    public bool AllowTextExpansionRead { get; set; } = true;

    public bool AllowTextExpansionWrite { get; set; } = true;

    public bool AllowTaskManage { get; set; } = true;

    public bool IsAllowed(McpSecuritySetting setting) => McpSecuritySettingCatalog.IsAllowed(this, setting);

    public void Set(McpSecuritySetting setting, bool value) => McpSecuritySettingCatalog.Set(this, setting, value);

    public void Normalize()
    {
        Paths ??= new McpPathSettings();
    }
}
