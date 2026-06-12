namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Serialization;
using FluentAssertions;

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
                CheckForUpdates = true
            },
            CrossMacroJsonContext.Default.ProfileSettings);
        await WriteJsonAsync(
            Path.Combine(defaultDirectory, ConfigFileNames.Hotkeys),
            new HotkeySettings
            {
                RecordingHotkey = "Ctrl+Alt+R",
                PlaybackHotkey = "Ctrl+Alt+P",
                PauseHotkey = "Ctrl+Alt+Space"
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
            new List<TextExpansion> { new(":mail", "me@example.com") },
            CrossMacroJsonContext.Default.ListTextExpansion);

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
            CrossMacroJsonContext.Default.ListTextExpansion);

        profileSettings.Should().BeEquivalentTo(new ProfileSettings());
        hotkeys.Should().BeEquivalentTo(new HotkeySettings());
        shortcuts.Should().BeEmpty();
        schedules.Should().BeEmpty();
        expansions.Should().BeEmpty();
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
