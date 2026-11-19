using CrossMacro.Core.Models.Profiles;
using CrossMacro.Infrastructure.Persistence.Settings;

namespace CrossMacro.Infrastructure.Tests.Services.Profiles;

public sealed class ProfileCatalogTests : IDisposable
{
    private readonly TempConfigDirectory _config = new("CrossMacroProfileCatalogTests_");
    private string _tempPath => _config.Path;

    public void Dispose()
    {
        _config.Dispose();
    }

    [Fact]
    public async Task CreateProfileAsync_CreatesEveryProfileConfigFile()
    {
        using var manager = new ProfileCatalog(_tempPath);
        await manager.InitializeAsync();

        var profile = await manager.CreateProfileAsync("Work");

        var directory = manager.GetProfileDirectory(profile.Id);
        string[] expectedFiles =
        [
            ConfigFileNames.Settings,
            ConfigFileNames.Hotkeys,
            ConfigFileNames.Shortcuts,
            ConfigFileNames.Triggers,
            ConfigFileNames.Schedules,
            ConfigFileNames.TextExpansions,
            ConfigFileNames.LoadedMacros,
        ];
        _ = expectedFiles.Where(file => !File.Exists(Path.Combine(directory, file))).Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProfileAsync_UsesInjectedTimeProviderForCreationMetadata()
    {
        var expected = new DateTimeOffset(2030, 2, 3, 4, 5, 6, TimeSpan.Zero);
        using var manager = new ProfileCatalog(_tempPath, new FakeTimeProvider(expected));
        await manager.InitializeAsync();

        var profile = await manager.CreateProfileAsync("Timed Profile");

        _ = profile.CreatedAt.Should().Be(expected.UtcDateTime);
    }

    [Fact]
    public void Dispose_CanBeCalledRepeatedly()
    {
        using var manager = new ProfileCatalog(_tempPath);

        var act = () =>
        {
            manager.Dispose();
            manager.Dispose();
        };

        _ = act.Should().NotThrow();
    }

    [Fact]
    public async Task CreateProfileAsync_WhenDefaultProfileHasUserData_CreatesCleanDefaultProfile()
    {
        using var manager = new ProfileCatalog(_tempPath);
        await manager.InitializeAsync();

        var defaultDirectory = manager.GetProfileDirectory("default");
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.Settings),
            new PersistedProfileSettings
            {
                PlaybackSpeed = 2.5,
                IsLooping = true,
                EnableTextExpansion = true,
                CheckForUpdates = true,
            },
            CrossMacroJsonContext.Default.PersistedProfileSettings);
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
            CrossMacroJsonContext.Default.PersistedProfileSettings);
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

        _ = profileSettings.Should().BeEquivalentTo(new PersistedProfileSettings());
        _ = hotkeys.Should().BeEquivalentTo(new HotkeySettings());
        _ = shortcuts.Should().BeEmpty();
        _ = schedules.Should().BeEmpty();
        _ = expansions.Should().BeEmpty();
        _ = Directory.Exists(Path.Combine(createdDirectory, ConfigFileNames.MacrosDirectory)).Should().BeTrue();
        _ = Directory.Exists(Path.Combine(defaultDirectory, ConfigFileNames.MacrosDirectory)).Should().BeTrue();
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

        using var manager = new ProfileCatalog(_tempPath);

        var act = () => manager.InitializeAsync();

        _ = await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*unsupported path characters*");
        _ = Directory.Exists(Path.Combine(_tempPath, "outside")).Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenConfigRootHasSymlinkedAncestor_UsesConfiguredTarget()
    {
        var outsideRoot = Path.Combine(_tempPath, "outside-target");
        var symlinkedAncestor = Path.Combine(_tempPath, "linked-parent");
        var configRoot = Path.Combine(symlinkedAncestor, "config");
        _ = Directory.CreateDirectory(outsideRoot);
        _ = Directory.CreateSymbolicLink(symlinkedAncestor, outsideRoot);

        using var manager = new ProfileCatalog(configRoot);

        await manager.InitializeAsync();

        _ = manager.ActiveProfile.Id.Should().Be("default");
        _ = Directory.Exists(Path.Combine(outsideRoot, "config", ConfigFileNames.ProfilesDirectory, "default")).Should().BeTrue();
        _ = File.Exists(Path.Combine(outsideRoot, "config", ConfigFileNames.ProfileRegistry)).Should().BeTrue();
    }

    [Fact]
    public async Task GetProfileDirectory_WhenProfileDirectoryIsSymlink_RejectsIt()
    {
        using var manager = new ProfileCatalog(_tempPath);
        await manager.InitializeAsync();
        var outsideRoot = Path.Combine(_tempPath, "outside-target");
        var profilePath = Path.Combine(_tempPath, ConfigFileNames.ProfilesDirectory, "work");
        _ = Directory.CreateDirectory(outsideRoot);
        _ = Directory.CreateSymbolicLink(profilePath, outsideRoot);

        var act = () => manager.GetProfileDirectory("work");

        _ = act.Should().Throw<InvalidDataException>()
            .WithMessage("*must not be a reparse point*");
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

        using var manager = new ProfileCatalog(_tempPath);
        await manager.InitializeAsync();

        var migratedPath = Path.Combine(
            manager.GetProfileDirectory("default"),
            ConfigFileNames.Triggers);
        var migrated = await ReadJsonAsync(
            migratedPath,
            CrossMacroJsonContext.Default.ListTriggerTask);

        _ = migrated.Should().ContainSingle();
        _ = migrated[0].Should().BeEquivalentTo(trigger);
    }

    private static async Task WriteJsonAsync<T>(string filePath, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var json = JsonSerializer.Serialize(value, typeInfo);
        await File.WriteAllTextAsync(filePath, json, TestContext.Current.CancellationToken);
    }

    private static async Task<T> ReadJsonAsync<T>(string filePath, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var json = await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken);
        return JsonSerializer.Deserialize(json, typeInfo)!;
    }
}
