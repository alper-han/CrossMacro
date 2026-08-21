namespace CrossMacro.Mcp.Contracts;

public enum McpCapability
{
    StatusRead = 0,
    MacroRead = 1,
    ScreenRead = 2,
    ClipboardRead = 3,
    ClipboardWrite = 4,
    InputAutomation = 5,
    Recording = 6,
    WindowRead = 7,
    WindowControl = 8,
    FileRead = 9,
    FileWrite = 10,
    CommandExecute = 11,
    ShellExecute = 12,
    SettingsRead = 13,
    SettingsWrite = 14,
    ProfileManage = 15,
    TextExpansionRead = 16,
    TextExpansionWrite = 17,
    TaskManage = 18,
    PrivilegeElevation = 19,
}
