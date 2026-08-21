namespace CrossMacro.Core.Models;

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

    public bool IsAllowed(McpSecuritySetting setting) => setting switch
    {
        McpSecuritySetting.MacroRead => AllowMacroRead,
        McpSecuritySetting.ScreenRead => AllowScreenRead,
        McpSecuritySetting.ClipboardRead => AllowClipboardRead,
        McpSecuritySetting.ClipboardWrite => AllowClipboardWrite,
        McpSecuritySetting.InputAutomation => AllowInputAutomation,
        McpSecuritySetting.Recording => AllowRecording,
        McpSecuritySetting.WindowRead => AllowWindowRead,
        McpSecuritySetting.WindowControl => AllowWindowControl,
        McpSecuritySetting.FileRead => AllowFileRead,
        McpSecuritySetting.FileWrite => AllowFileWrite,
        McpSecuritySetting.CommandExecute => AllowCommandExecute,
        McpSecuritySetting.ShellExecute => AllowShellExecute,
        McpSecuritySetting.SettingsRead => AllowSettingsRead,
        McpSecuritySetting.SettingsWrite => AllowSettingsWrite,
        McpSecuritySetting.ProfileManage => AllowProfileManage,
        McpSecuritySetting.TextExpansionRead => AllowTextExpansionRead,
        McpSecuritySetting.TextExpansionWrite => AllowTextExpansionWrite,
        McpSecuritySetting.TaskManage => AllowTaskManage,
        McpSecuritySetting.PrivilegeElevation => AllowPrivilegeElevation,
        _ => false,
    };

    public void Set(McpSecuritySetting setting, bool value)
    {
        switch (setting)
        {
            case McpSecuritySetting.MacroRead: AllowMacroRead = value; break;
            case McpSecuritySetting.ScreenRead: AllowScreenRead = value; break;
            case McpSecuritySetting.ClipboardRead: AllowClipboardRead = value; break;
            case McpSecuritySetting.ClipboardWrite: AllowClipboardWrite = value; break;
            case McpSecuritySetting.InputAutomation: AllowInputAutomation = value; break;
            case McpSecuritySetting.Recording: AllowRecording = value; break;
            case McpSecuritySetting.WindowRead: AllowWindowRead = value; break;
            case McpSecuritySetting.WindowControl: AllowWindowControl = value; break;
            case McpSecuritySetting.FileRead: AllowFileRead = value; break;
            case McpSecuritySetting.FileWrite: AllowFileWrite = value; break;
            case McpSecuritySetting.CommandExecute: AllowCommandExecute = value; break;
            case McpSecuritySetting.ShellExecute: AllowShellExecute = value; break;
            case McpSecuritySetting.SettingsRead: AllowSettingsRead = value; break;
            case McpSecuritySetting.SettingsWrite: AllowSettingsWrite = value; break;
            case McpSecuritySetting.ProfileManage: AllowProfileManage = value; break;
            case McpSecuritySetting.TextExpansionRead: AllowTextExpansionRead = value; break;
            case McpSecuritySetting.TextExpansionWrite: AllowTextExpansionWrite = value; break;
            case McpSecuritySetting.TaskManage: AllowTaskManage = value; break;
            case McpSecuritySetting.PrivilegeElevation: AllowPrivilegeElevation = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown MCP security setting.");
        }
    }

    public void Normalize()
    {
        Paths ??= new McpPathSettings();
        ApprovalTimeoutSeconds = NormalizeApprovalTimeoutSeconds(ApprovalTimeoutSeconds);
    }

    public static int NormalizeApprovalTimeoutSeconds(int value) =>
        Math.Clamp(value, MinimumApprovalTimeoutSeconds, MaximumApprovalTimeoutSeconds);
}
