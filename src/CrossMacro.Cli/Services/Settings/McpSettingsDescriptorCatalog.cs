namespace CrossMacro.Cli.Services.Settings;

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

        foreach (var setting in Enum.GetValues<McpPathSetting>())
        {
            AddRootDescriptor(descriptors, McpSettingsKeys.GetRootsKey(setting), setting);
        }
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
            (settings, defaults) => settings.Paths = settings.Paths.WithRoots(setting, defaults.Paths.GetRoots(setting)))
        {
            Sensitive = true,
        });
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
        if (!CliValueTokens.TryParseBool(rawValue, out var value))
        {
            return (false, $"Invalid boolean value for {key}: {rawValue}");
        }

        setter(value);
        return (true, string.Empty);
    }
}
