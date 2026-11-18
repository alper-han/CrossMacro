namespace CrossMacro.Core.Models.Settings;

/// <summary>
/// Persisted local MCP capability switches. Capabilities are available by default
/// and can be restricted explicitly through MCP settings and path roots.
/// </summary>
public sealed class McpSecuritySettings
{
    public const int DefaultApprovalTimeoutSeconds = 30;
    public const int MinimumApprovalTimeoutSeconds = 1;
    public const int MaximumApprovalTimeoutSeconds = 300;

    public McpPathSettings Paths { get; set; } = new();

    public int ApprovalTimeoutSeconds { get; set; } = DefaultApprovalTimeoutSeconds;

    public bool AllowMacroRead { get; set; } = true;

    public bool AllowScreenRead { get; set; } = true;

    public bool AllowClipboardRead { get; set; } = true;

    public bool AllowClipboardWrite { get; set; } = true;

    public bool AllowInputAutomation { get; set; } = true;

    public bool AllowRecording { get; set; } = true;

    public bool AllowWindowRead { get; set; } = true;

    public bool AllowWindowControl { get; set; } = true;

    public bool AllowFileRead { get; set; } = true;

    public bool AllowFileWrite { get; set; } = true;

    public bool AllowCommandExecute { get; set; } = true;

    public bool AllowShellExecute { get; set; } = true;

    public bool AllowSettingsRead { get; set; } = true;

    public bool AllowSettingsWrite { get; set; } = true;

    public bool AllowProfileManage { get; set; } = true;

    public bool AllowTextExpansionRead { get; set; } = true;

    public bool AllowTextExpansionWrite { get; set; } = true;

    public bool AllowTaskManage { get; set; } = true;

    public bool AllowPrivilegeElevation { get; set; }

    public bool IsAllowed(McpSecuritySetting setting) => McpSecuritySettingCatalog.IsAllowed(this, setting);

    public void Set(McpSecuritySetting setting, bool value) => McpSecuritySettingCatalog.Set(this, setting, value);

    public void Normalize()
    {
        Paths ??= new McpPathSettings();
        ApprovalTimeoutSeconds = NormalizeApprovalTimeoutSeconds(ApprovalTimeoutSeconds);
    }

    public static int NormalizeApprovalTimeoutSeconds(int value) =>
        Math.Clamp(value, MinimumApprovalTimeoutSeconds, MaximumApprovalTimeoutSeconds);
}
