namespace CrossMacro.Core.Models.Settings;

/// <summary>Single source of truth tying each MCP capability to its stable key and settings property.</summary>
public static class McpSecuritySettingCatalog
{
    private sealed record Entry(
        McpSecuritySetting Setting,
        string Key,
        Func<McpSecuritySettings, bool> Read,
        Action<McpSecuritySettings, bool> Write);

    private static readonly Entry[] Entries =
    [
        new(McpSecuritySetting.MacroRead, "mcp.macroRead", static s => s.AllowMacroRead, static (s, v) => s.AllowMacroRead = v),
        new(McpSecuritySetting.ScreenRead, "mcp.screenRead", static s => s.AllowScreenRead, static (s, v) => s.AllowScreenRead = v),
        new(McpSecuritySetting.ClipboardRead, "mcp.clipboardRead", static s => s.AllowClipboardRead, static (s, v) => s.AllowClipboardRead = v),
        new(McpSecuritySetting.ClipboardWrite, "mcp.clipboardWrite", static s => s.AllowClipboardWrite, static (s, v) => s.AllowClipboardWrite = v),
        new(McpSecuritySetting.InputAutomation, "mcp.inputAutomation", static s => s.AllowInputAutomation, static (s, v) => s.AllowInputAutomation = v),
        new(McpSecuritySetting.Recording, "mcp.recording", static s => s.AllowRecording, static (s, v) => s.AllowRecording = v),
        new(McpSecuritySetting.WindowRead, "mcp.windowRead", static s => s.AllowWindowRead, static (s, v) => s.AllowWindowRead = v),
        new(McpSecuritySetting.WindowControl, "mcp.windowControl", static s => s.AllowWindowControl, static (s, v) => s.AllowWindowControl = v),
        new(McpSecuritySetting.FileRead, "mcp.fileRead", static s => s.AllowFileRead, static (s, v) => s.AllowFileRead = v),
        new(McpSecuritySetting.FileWrite, "mcp.fileWrite", static s => s.AllowFileWrite, static (s, v) => s.AllowFileWrite = v),
        new(McpSecuritySetting.CommandExecute, "mcp.commandExecute", static s => s.AllowCommandExecute, static (s, v) => s.AllowCommandExecute = v),
        new(McpSecuritySetting.ShellExecute, "mcp.shellExecute", static s => s.AllowShellExecute, static (s, v) => s.AllowShellExecute = v),
        new(McpSecuritySetting.SettingsRead, "mcp.settingsRead", static s => s.AllowSettingsRead, static (s, v) => s.AllowSettingsRead = v),
        new(McpSecuritySetting.SettingsWrite, "mcp.settingsWrite", static s => s.AllowSettingsWrite, static (s, v) => s.AllowSettingsWrite = v),
        new(McpSecuritySetting.ProfileManage, "mcp.profileManage", static s => s.AllowProfileManage, static (s, v) => s.AllowProfileManage = v),
        new(McpSecuritySetting.TextExpansionRead, "mcp.textExpansionRead", static s => s.AllowTextExpansionRead, static (s, v) => s.AllowTextExpansionRead = v),
        new(McpSecuritySetting.TextExpansionWrite, "mcp.textExpansionWrite", static s => s.AllowTextExpansionWrite, static (s, v) => s.AllowTextExpansionWrite = v),
        new(McpSecuritySetting.TaskManage, "mcp.taskManage", static s => s.AllowTaskManage, static (s, v) => s.AllowTaskManage = v),
    ];

    private static readonly Dictionary<McpSecuritySetting, Entry> BySetting = Entries.ToDictionary(static entry => entry.Setting);
    private static readonly Dictionary<string, Entry> ByKey = Entries.ToDictionary(static entry => entry.Key, StringComparer.Ordinal);

    public static IReadOnlyList<McpSecuritySetting> Values { get; } = Array.AsReadOnly([.. Entries.Select(static entry => entry.Setting)]);

    public static string GetKey(McpSecuritySetting setting) => Find(setting).Key;

    public static bool TryParse(string key, out McpSecuritySetting setting)
    {
        if (key is not null && ByKey.TryGetValue(key, out var entry))
        {
            setting = entry.Setting;
            return true;
        }

        setting = default;
        return false;
    }

    internal static bool IsAllowed(McpSecuritySettings settings, McpSecuritySetting setting) =>
        BySetting.TryGetValue(setting, out var entry) && entry.Read(settings);

    internal static void Set(McpSecuritySettings settings, McpSecuritySetting setting, bool value) =>
        Find(setting).Write(settings, value);

    private static Entry Find(McpSecuritySetting setting) =>
        BySetting.TryGetValue(setting, out var entry)
            ? entry
            : throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown MCP security setting.");
}
