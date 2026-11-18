namespace CrossMacro.Cli.Services.Settings;

internal sealed class McpSettingsDescriptor(
    string key,
    Func<McpSecuritySettings, object?> getValue,
    Func<McpSecuritySettings, string, string, (bool Success, string Error)> trySetValue,
    Action<McpSecuritySettings, McpSecuritySettings> resetValue)
{
    public string Key { get; } = key;

    /// <summary>True when the value must never be echoed to a remote client.</summary>
    public bool Sensitive { get; init; }

    public object? GetValue(McpSecuritySettings settings) => getValue(settings);

    public bool TrySetValue(McpSecuritySettings settings, string rawValue, out string errorMessage)
    {
        var result = trySetValue(settings, rawValue, Key);
        errorMessage = result.Error;
        return result.Success;
    }

    public void ResetValue(McpSecuritySettings settings, McpSecuritySettings defaults) => resetValue(settings, defaults);
}
