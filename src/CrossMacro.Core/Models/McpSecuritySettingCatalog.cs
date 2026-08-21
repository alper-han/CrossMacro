namespace CrossMacro.Core.Models;

public static class McpSecuritySettingCatalog
{
    public static IReadOnlyList<McpSecuritySetting> Values { get; } =
    [
        McpSecuritySetting.MacroRead,
        McpSecuritySetting.ScreenRead,
        McpSecuritySetting.ClipboardRead,
        McpSecuritySetting.ClipboardWrite,
        McpSecuritySetting.InputAutomation,
        McpSecuritySetting.Recording,
        McpSecuritySetting.WindowRead,
        McpSecuritySetting.WindowControl,
        McpSecuritySetting.FileRead,
        McpSecuritySetting.FileWrite,
        McpSecuritySetting.CommandExecute,
        McpSecuritySetting.ShellExecute,
        McpSecuritySetting.SettingsRead,
        McpSecuritySetting.SettingsWrite,
        McpSecuritySetting.ProfileManage,
        McpSecuritySetting.TextExpansionRead,
        McpSecuritySetting.TextExpansionWrite,
        McpSecuritySetting.TaskManage,
        McpSecuritySetting.PrivilegeElevation,
    ];

    public static string GetKey(McpSecuritySetting setting) => setting switch
    {
        McpSecuritySetting.MacroRead => "mcp.macroRead",
        McpSecuritySetting.ScreenRead => "mcp.screenRead",
        McpSecuritySetting.ClipboardRead => "mcp.clipboardRead",
        McpSecuritySetting.ClipboardWrite => "mcp.clipboardWrite",
        McpSecuritySetting.InputAutomation => "mcp.inputAutomation",
        McpSecuritySetting.Recording => "mcp.recording",
        McpSecuritySetting.WindowRead => "mcp.windowRead",
        McpSecuritySetting.WindowControl => "mcp.windowControl",
        McpSecuritySetting.FileRead => "mcp.fileRead",
        McpSecuritySetting.FileWrite => "mcp.fileWrite",
        McpSecuritySetting.CommandExecute => "mcp.commandExecute",
        McpSecuritySetting.ShellExecute => "mcp.shellExecute",
        McpSecuritySetting.SettingsRead => "mcp.settingsRead",
        McpSecuritySetting.SettingsWrite => "mcp.settingsWrite",
        McpSecuritySetting.ProfileManage => "mcp.profileManage",
        McpSecuritySetting.TextExpansionRead => "mcp.textExpansionRead",
        McpSecuritySetting.TextExpansionWrite => "mcp.textExpansionWrite",
        McpSecuritySetting.TaskManage => "mcp.taskManage",
        McpSecuritySetting.PrivilegeElevation => "mcp.privilegeElevation",
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown MCP security setting."),
    };

    public static bool TryParse(string key, out McpSecuritySetting setting) => key switch
    {
        "mcp.macroRead" => Set(McpSecuritySetting.MacroRead, out setting),
        "mcp.screenRead" => Set(McpSecuritySetting.ScreenRead, out setting),
        "mcp.clipboardRead" => Set(McpSecuritySetting.ClipboardRead, out setting),
        "mcp.clipboardWrite" => Set(McpSecuritySetting.ClipboardWrite, out setting),
        "mcp.inputAutomation" => Set(McpSecuritySetting.InputAutomation, out setting),
        "mcp.recording" => Set(McpSecuritySetting.Recording, out setting),
        "mcp.windowRead" => Set(McpSecuritySetting.WindowRead, out setting),
        "mcp.windowControl" => Set(McpSecuritySetting.WindowControl, out setting),
        "mcp.fileRead" => Set(McpSecuritySetting.FileRead, out setting),
        "mcp.fileWrite" => Set(McpSecuritySetting.FileWrite, out setting),
        "mcp.commandExecute" => Set(McpSecuritySetting.CommandExecute, out setting),
        "mcp.shellExecute" => Set(McpSecuritySetting.ShellExecute, out setting),
        "mcp.settingsRead" => Set(McpSecuritySetting.SettingsRead, out setting),
        "mcp.settingsWrite" => Set(McpSecuritySetting.SettingsWrite, out setting),
        "mcp.profileManage" => Set(McpSecuritySetting.ProfileManage, out setting),
        "mcp.textExpansionRead" => Set(McpSecuritySetting.TextExpansionRead, out setting),
        "mcp.textExpansionWrite" => Set(McpSecuritySetting.TextExpansionWrite, out setting),
        "mcp.taskManage" => Set(McpSecuritySetting.TaskManage, out setting),
        "mcp.privilegeElevation" => Set(McpSecuritySetting.PrivilegeElevation, out setting),
        _ => Set(default, out setting, result: false),
    };

    private static bool Set(McpSecuritySetting value, out McpSecuritySetting setting, bool result = true)
    {
        setting = value;
        return result;
    }
}
