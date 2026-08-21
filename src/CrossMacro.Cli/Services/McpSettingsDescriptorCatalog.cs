namespace CrossMacro.Cli.Services;

internal static class McpSettingsDescriptorCatalog
{
    public static IReadOnlyList<McpSettingsDescriptor> Values { get; } = CreateDescriptors();

    public static IReadOnlyList<string> Keys { get; } = Values.Select(static descriptor => descriptor.Key).ToArray();

    public static bool TryGet(string key, out McpSettingsDescriptor descriptor)
    {
        descriptor = Values.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal))!;
        return descriptor is not null;
    }

    private static IReadOnlyList<McpSettingsDescriptor> CreateDescriptors()
    {
        var descriptors = McpSecuritySettingCatalog.Values
            .Select(CreateCapabilityDescriptor)
            .ToList();

        descriptors.Add(new McpSettingsDescriptor(
            McpSettingsKeys.ApprovalTimeoutSeconds,
            settings => settings.ApprovalTimeoutSeconds,
            (settings, rawValue, key) =>
            {
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout)
                    || timeout is < McpSecuritySettings.MinimumApprovalTimeoutSeconds or > McpSecuritySettings.MaximumApprovalTimeoutSeconds)
                {
                    return (false, $"Invalid integer value for {key}: {rawValue}. Expected {McpSecuritySettings.MinimumApprovalTimeoutSeconds}..{McpSecuritySettings.MaximumApprovalTimeoutSeconds}.");
                }

                settings.ApprovalTimeoutSeconds = timeout;
                return (true, string.Empty);
            },
            static (settings, defaults) => settings.ApprovalTimeoutSeconds = defaults.ApprovalTimeoutSeconds));

        AddRootDescriptor(descriptors, McpSettingsKeys.MacroReadRoots, McpPathSetting.MacroRead);
        AddRootDescriptor(descriptors, McpSettingsKeys.MacroWriteRoots, McpPathSetting.MacroWrite);
        AddRootDescriptor(descriptors, McpSettingsKeys.ImageReadRoots, McpPathSetting.ImageRead);
        AddRootDescriptor(descriptors, McpSettingsKeys.ImageWriteRoots, McpPathSetting.ImageWrite);
        AddRootDescriptor(descriptors, McpSettingsKeys.FileReadRoots, McpPathSetting.FileRead);
        AddRootDescriptor(descriptors, McpSettingsKeys.FileWriteRoots, McpPathSetting.FileWrite);
        return descriptors;
    }

    private static McpSettingsDescriptor CreateCapabilityDescriptor(McpSecuritySetting setting)
    {
        var key = McpSecuritySettingCatalog.GetKey(setting);
        return new McpSettingsDescriptor(
            key,
            settings => settings.IsAllowed(setting),
            (settings, rawValue, descriptorKey) => TrySetBool(
                rawValue,
                value => settings.Set(setting, value),
                descriptorKey),
            (settings, defaults) => settings.Set(setting, defaults.IsAllowed(setting)));
    }

    private static void AddRootDescriptor(List<McpSettingsDescriptor> descriptors, string key, McpPathSetting setting)
    {
        descriptors.Add(new McpSettingsDescriptor(
            key,
            settings => string.Join(';', settings.Paths.GetRoots(setting)),
            (settings, rawValue, descriptorKey) => TrySetRoots(settings, setting, rawValue, descriptorKey),
            (settings, defaults) => settings.Paths = settings.Paths.WithRoots(setting, defaults.Paths.GetRoots(setting))));
    }

    private static (bool Success, string Error) TrySetRoots(
        McpSecuritySettings settings,
        McpPathSetting setting,
        string rawValue,
        string key)
    {
        var values = rawValue
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (values.Exists(static value => !Path.IsPathFullyQualified(value)))
        {
            return (false, $"Invalid path value for {key}: every root must be absolute.");
        }

        settings.Paths = settings.Paths.WithRoots(setting, values);
        return (true, string.Empty);
    }

    private static (bool Success, string Error) TrySetBool(
        string rawValue,
        Action<bool> setter,
        string key)
    {
        if (!bool.TryParse(rawValue, out var value))
        {
            if (string.Equals(rawValue, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, "on", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
            }
            else if (string.Equals(rawValue, "0", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, "no", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, "off", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
            }
            else
            {
                return (false, $"Invalid boolean value for {key}: {rawValue}");
            }
        }

        setter(value);
        return (true, string.Empty);
    }
}
