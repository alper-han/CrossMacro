namespace CrossMacro.Mcp.Tools;

public sealed class McpSettingsTools(ISettingsCliService settingsCliService, McpToolAuthorization authorization)
{
    private readonly ISettingsCliService _settingsCliService = settingsCliService;
    private readonly McpToolAuthorization _authorization = authorization;

    [McpServerTool(Name = "settings.get", Title = "Get settings", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpSettingsResult))]
    [Description("Reads one setting or all supported settings. Sensitive values are redacted.")]
    public async Task<McpSettingsResult> GetSettingsAsync(string? key = null, bool all = false, CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.SettingsRead);
        if (capability is not null)
        {
            return Create("get", capability, data: null);
        }

        var result = await _settingsCliService.GetAsync(all ? null : key, cancellationToken).ConfigureAwait(false);
        return Create("get", McpToolOutcomeMapper.FromSettingsResult(result), result.Data);
    }

    [McpServerTool(Name = "settings.set", Title = "Set a setting", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpSettingsResult))]
    [Description("Updates one supported CrossMacro setting.")]
    public async Task<McpSettingsResult> SetSettingsAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        var capability = _authorization.Require(McpCapability.SettingsWrite);
        if (capability is not null)
        {
            return Create("set", capability, data: null);
        }

        if (McpSettingsKeys.IsPolicyKey(key))
        {
            return Create("set", McpToolOutcomeMapper.Denied("MCP security settings can only be changed outside an MCP session."), data: null);
        }

        var result = await _settingsCliService.SetAsync(key, value, cancellationToken).ConfigureAwait(false);
        return Create("set", McpToolOutcomeMapper.FromSettingsResult(result), result.Data);
    }

    [McpServerTool(Name = "settings.list_keys", Title = "List setting keys", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpSettingsResult))]
    [Description("Lists supported CrossMacro settings keys.")]
    public async Task<McpSettingsResult> ListSettingsKeysAsync(CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.SettingsRead);
        if (capability is not null)
        {
            return Create("list_keys", capability, data: null);
        }

        var result = await _settingsCliService.ListKeysAsync(cancellationToken).ConfigureAwait(false);
        return Create("list_keys", McpToolOutcomeMapper.FromSettingsResult(result), result.Data);
    }

    [McpServerTool(Name = "settings.reset", Title = "Reset a setting", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpSettingsResult))]
    [Description("Resets one supported CrossMacro setting to its default value.")]
    public async Task<McpSettingsResult> ResetSettingsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var capability = _authorization.Require(McpCapability.SettingsWrite);
        if (capability is not null)
        {
            return Create("reset", capability, data: null);
        }

        if (McpSettingsKeys.IsPolicyKey(key))
        {
            return Create("reset", McpToolOutcomeMapper.Denied("MCP security settings can only be changed outside an MCP session."), data: null);
        }

        var result = await _settingsCliService.ResetAsync(key, cancellationToken).ConfigureAwait(false);
        return Create("reset", McpToolOutcomeMapper.FromSettingsResult(result), result.Data);
    }

    private static McpSettingsResult Create(string action, McpToolOutcome outcome, object? data)
    {
        var settings = new List<McpSettingEntry>();
        var keys = new List<string>();
        if (data is IReadOnlyDictionary<string, object?> values)
        {
            settings.AddRange(values.Select(static pair => ToSettingEntry(pair.Key, pair.Value)));
        }
        else if (data is SettingsValueData value)
        {
            settings.Add(ToSettingEntry(value.Key, value.Value));
        }
        else if (data is SettingsMutationData mutation)
        {
            settings.Add(ToSettingEntry(mutation.Key, mutation.NewValue));
        }
        else if (data is IEnumerable<string> keyValues)
        {
            keys.AddRange(keyValues);
        }

        return new(action, outcome, settings.AsReadOnly(), keys.AsReadOnly());
    }

    private static McpSettingEntry ToSettingEntry(string key, object? value)
    {
        var redacted = key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("password", StringComparison.OrdinalIgnoreCase);
        return new(key, redacted ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), redacted);
    }
}
