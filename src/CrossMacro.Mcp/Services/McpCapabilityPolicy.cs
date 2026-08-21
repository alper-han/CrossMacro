namespace CrossMacro.Mcp.Services;

public sealed class McpCapabilityPolicy(ISettingsService settingsService) : IMcpCapabilityPolicy
{
    private readonly ISettingsService _settingsService = settingsService;
    private int _restricted;

    public bool IsRestricted => Volatile.Read(ref _restricted) is 1;

    public bool IsAllowed(McpCapability capability)
    {
        if (IsRestricted && capability is not McpCapability.StatusRead and not McpCapability.MacroRead)
        {
            return false;
        }

        var settings = _settingsService.Current.McpSecurity ?? new McpSecuritySettings();
        return capability switch
        {
            McpCapability.StatusRead => true,
            McpCapability.MacroRead => settings.AllowMacroRead,
            McpCapability.ScreenRead => settings.AllowScreenRead,
            McpCapability.ClipboardRead => settings.AllowClipboardRead,
            McpCapability.ClipboardWrite => settings.AllowClipboardWrite,
            McpCapability.InputAutomation => settings.AllowInputAutomation,
            McpCapability.Recording => settings.AllowRecording,
            McpCapability.WindowRead => settings.AllowWindowRead,
            McpCapability.WindowControl => settings.AllowWindowControl,
            McpCapability.FileRead => settings.AllowFileRead,
            McpCapability.FileWrite => settings.AllowFileWrite,
            McpCapability.CommandExecute => settings.AllowCommandExecute,
            McpCapability.ShellExecute => settings.AllowShellExecute,
            McpCapability.SettingsRead => settings.AllowSettingsRead,
            McpCapability.SettingsWrite => settings.AllowSettingsWrite,
            McpCapability.ProfileManage => settings.AllowProfileManage,
            McpCapability.TextExpansionRead => settings.AllowTextExpansionRead,
            McpCapability.TextExpansionWrite => settings.AllowTextExpansionWrite,
            McpCapability.TaskManage => settings.AllowTaskManage,
            McpCapability.PrivilegeElevation => settings.AllowPrivilegeElevation,
            _ => false,
        };
    }

    public bool IsAnyAllowed(params McpCapability[] capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        return capabilities.Any(IsAllowed);
    }

    public McpToolOutcome Require(McpCapability capability)
    {
        if (IsAllowed(capability))
        {
            return McpToolOutcomeMapper.Success(string.Empty);
        }

        var detail = IsRestricted
            ? "MCP is running in restricted mode."
            : "Enable the capability in CrossMacro MCP security settings.";
        return McpToolOutcomeMapper.Denied("MCP capability is not enabled for this runtime.", detail);
    }

    public void SetRestricted(bool restricted) => Volatile.Write(ref _restricted, restricted ? 1 : 0);
}
