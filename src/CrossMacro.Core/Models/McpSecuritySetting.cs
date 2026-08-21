namespace CrossMacro.Core.Models;

public enum McpSecuritySetting
{
    MacroRead = 0,
    ScreenRead = 1,
    ClipboardRead = 2,
    ClipboardWrite = 3,
    InputAutomation = 4,
    Recording = 5,
    WindowRead = 6,
    WindowControl = 7,
    FileRead = 8,
    FileWrite = 9,
    CommandExecute = 10,
    ShellExecute = 11,
    PrivilegeElevation = 12,
    SettingsRead = 13,
    SettingsWrite = 14,
    ProfileManage = 15,
    TextExpansionRead = 16,
    TextExpansionWrite = 17,
    TaskManage = 18,
}
