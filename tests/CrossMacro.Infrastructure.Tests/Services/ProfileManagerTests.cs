namespace CrossMacro.Infrastructure.Tests.Services;


public sealed class ProfileManagerTests : IDisposable
{
    private readonly string _tempPath;

    public ProfileManagerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "CrossMacroProfileManagerTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, recursive: true);
        }
    }

    [Fact]
    public async Task CreateProfileAsync_WhenDefaultProfileHasUserData_CreatesCleanDefaultProfile()
    {
        var manager = new ProfileManager(_tempPath);
        await manager.InitializeAsync();

        var defaultDirectory = manager.GetProfileDirectory("default");
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.Settings),
            new ProfileSettings
            {
                PlaybackSpeed = 2.5,
                IsLooping = true,
                EnableTextExpansion = true,
                CheckForUpdates = true,
            },
            CrossMacroJsonContext.Default.ProfileSettings);
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.Hotkeys),
            new HotkeySettings
            {
                RecordingHotkey = "Ctrl+Alt+R",
                PlaybackHotkey = "Ctrl+Alt+P",
                PauseHotkey = "Ctrl+Alt+Space",
            },
            CrossMacroJsonContext.Default.HotkeySettings);
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.Shortcuts),
            new List<ShortcutTask> { new() { Name = "Copied shortcut" } },
            CrossMacroJsonContext.Default.ListShortcutTask);
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.Schedules),
            new List<ScheduledTask> { new() { Name = "Copied schedule" } },
            CrossMacroJsonContext.Default.ListScheduledTask);
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.TextExpansions),
            new List<TextExpansionEntry> { new(":mail", "me@example.com") },
            CrossMacroJsonContext.Default.ListTextExpansionEntry);

        var created = await manager.CreateProfileAsync("Clean Profile");
        var createdDirectory = manager.GetProfileDirectory(created.Id);

        var profileSettings = await ReadJsonAsync(
            Path.Combine(createdDirectory, ConfigFileNames.Settings),
            CrossMacroJsonContext.Default.ProfileSettings);
        var hotkeys = await ReadJsonAsync(
            Path.Combine(createdDirectory, ConfigFileNames.Hotkeys),
            CrossMacroJsonContext.Default.HotkeySettings);
        var shortcuts = await ReadJsonAsync(
            Path.Combine(createdDirectory, ConfigFileNames.Shortcuts),
            CrossMacroJsonContext.Default.ListShortcutTask);
        var schedules = await ReadJsonAsync(
            Path.Combine(createdDirectory, ConfigFileNames.Schedules),
            CrossMacroJsonContext.Default.ListScheduledTask);
        var expansions = await ReadJsonAsync(
            Path.Combine(createdDirectory, ConfigFileNames.TextExpansions),
            CrossMacroJsonContext.Default.ListTextExpansionEntry);

        profileSettings.Should().BeEquivalentTo(new ProfileSettings());
        hotkeys.Should().BeEquivalentTo(new HotkeySettings());
        shortcuts.Should().BeEmpty();
        schedules.Should().BeEmpty();
        expansions.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_WhenRegistryContainsTraversalProfileId_RejectsItBeforePathUse()
    {
        var registryPath = Path.Combine(_tempPath, ConfigFileNames.ProfileRegistry);
        await WriteJsonAsync(
            registryPath,
            new ProfileRegistry
            {
                Profiles = { new ProfileInfo { Id = "../outside", Name = "Outside" } },
            },
            CrossMacroJsonContext.Default.ProfileRegistry);

        var manager = new ProfileManager(_tempPath);

        var act = () => manager.InitializeAsync();

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*unsupported path characters*");
        Directory.Exists(Path.Combine(_tempPath, "outside")).Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenConfigRootHasSymlinkedAncestor_RejectsBeforeCreatingOutsideRoot()
    {
        var outsideRoot = Path.Combine(_tempPath, "outside-target");
        var symlinkedAncestor = Path.Combine(_tempPath, "linked-parent");
        var configRoot = Path.Combine(symlinkedAncestor, "config");
        Directory.CreateDirectory(outsideRoot);
        Directory.CreateSymbolicLink(symlinkedAncestor, outsideRoot);

        var manager = new ProfileManager(configRoot);

        var act = () => manager.InitializeAsync();

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*reparse point*");
        Directory.Exists(Path.Combine(outsideRoot, "config")).Should().BeFalse();
        File.Exists(Path.Combine(outsideRoot, ConfigFileNames.ProfileRegistry)).Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_MigratesLegacyTriggersIntoDefaultProfile()
    {
        await WriteJsonAsync(
            Path.Combine(_tempPath, ConfigFileNames.Settings),
            new AppSettings(),
            CrossMacroJsonContext.Default.AppSettings);
        var trigger = new TriggerTask
        {
            Name = "Legacy trigger",
            Field = TriggerField.WindowTitle,
            MatchMode = TriggerMatchMode.Contains,
            Value = "Editor",
            Action = TriggerOperation.SwitchProfile,
        };
        await WriteJsonAsync(
            Path.Combine(_tempPath, ConfigFileNames.Triggers),
            new List<TriggerTask> { trigger },
            CrossMacroJsonContext.Default.ListTriggerTask);

        var manager = new ProfileManager(_tempPath);
        await manager.InitializeAsync();

        var migratedPath = Path.Combine(
            manager.GetProfileDirectory("default"),
            ConfigFileNames.Triggers);
        var migrated = await ReadJsonAsync(
            migratedPath,
            CrossMacroJsonContext.Default.ListTriggerTask);

        migrated.Should().ContainSingle();
        migrated[0].Should().BeEquivalentTo(trigger);
    }

    private static async Task WriteJsonAsync<T>(string filePath, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var json = JsonSerializer.Serialize(value, typeInfo);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static async Task<T> ReadJsonAsync<T>(string filePath, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize(json, typeInfo)!;
    }
}
