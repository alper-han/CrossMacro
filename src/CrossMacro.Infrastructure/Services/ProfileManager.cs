using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Helpers;
using CrossMacro.Infrastructure.Serialization;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

public class ProfileManager : IProfileManager
{
    private const string DefaultProfileId = "default";
    private const string DefaultProfileName = "Default";

    private static readonly string[] DefaultProfileConfigFiles =
    [
        ConfigFileNames.Settings,
        ConfigFileNames.Hotkeys,
        ConfigFileNames.Shortcuts,
        ConfigFileNames.Schedules,
        ConfigFileNames.TextExpansions
    ];

    private static readonly string[] MigratedProfileConfigFiles =
    [
        ConfigFileNames.Hotkeys,
        ConfigFileNames.Shortcuts,
        ConfigFileNames.Schedules,
        ConfigFileNames.TextExpansions
    ];

    private readonly string _configRootPath;
    private readonly string _profilesRootPath;
    private readonly string _registryFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Runtime service dependencies for live profile switching
    private readonly ISettingsService? _settingsService;
    private readonly IHotkeyConfigurationService? _hotkeyConfigService;
    private readonly HotkeySettings? _hotkeySettings;
    private readonly IGlobalHotkeyService? _hotkeyService;
    private readonly IShortcutService? _shortcutService;
    private readonly ISchedulerService? _schedulerService;
    private readonly ITextExpansionService? _textExpansionService;
    private readonly IScheduledTaskRepository? _scheduledTaskRepository;
    private readonly ITextExpansionStorageService? _textExpansionStorageService;

    private ProfileRegistry _registry = new();

    public ProfileInfo ActiveProfile { get; private set; } = new();

    public IReadOnlyList<ProfileInfo> Profiles { get; private set; } = [];

    public event EventHandler<ProfileInfo>? ProfileChanged;

    public ProfileManager() : this(configRootPath: null)
    {
    }

    public ProfileManager(string? configRootPath)
    {
        _configRootPath = string.IsNullOrWhiteSpace(configRootPath)
            ? PathHelper.GetConfigDirectory()
            : configRootPath;

        _profilesRootPath = Path.Combine(_configRootPath, ConfigFileNames.ProfilesDirectory);
        _registryFilePath = Path.Combine(_configRootPath, ConfigFileNames.ProfileRegistry);
    }

    public ProfileManager(
        string? configRootPath,
        ISettingsService settingsService,
        IHotkeyConfigurationService hotkeyConfigService,
        HotkeySettings hotkeySettings,
        IGlobalHotkeyService? hotkeyService,
        IShortcutService? shortcutService,
        ISchedulerService? schedulerService,
        ITextExpansionService? textExpansionService,
        IScheduledTaskRepository scheduledTaskRepository,
        ITextExpansionStorageService textExpansionStorageService)
        : this(configRootPath)
    {
        _settingsService = settingsService;
        _hotkeyConfigService = hotkeyConfigService;
        _hotkeySettings = hotkeySettings;
        _hotkeyService = hotkeyService;
        _shortcutService = shortcutService;
        _schedulerService = schedulerService;
        _textExpansionService = textExpansionService;
        _scheduledTaskRepository = scheduledTaskRepository;
        _textExpansionStorageService = textExpansionStorageService;
    }

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_configRootPath);
            Directory.CreateDirectory(_profilesRootPath);

            if (File.Exists(_registryFilePath))
            {
                _registry = await LoadRegistryAsync().ConfigureAwait(false);
                await EnsureDefaultProfileDirectoryAsync().ConfigureAwait(false);
                NormalizeRegistry();
                await SaveRegistryAsync().ConfigureAwait(false);
                Log.Information("Profile registry loaded from {Path}", _registryFilePath);
            }
            else if (File.Exists(GetRootConfigPath(ConfigFileNames.Settings)))
            {
                _registry = await MigrateFlatConfigurationAsync().ConfigureAwait(false);
                await SaveRegistryAsync().ConfigureAwait(false);
                Log.Information("Migrated flat configuration to default profile");
            }
            else
            {
                _registry = await CreateFreshDefaultProfileAsync().ConfigureAwait(false);
                await SaveRegistryAsync().ConfigureAwait(false);
                Log.Information("Created fresh default profile configuration");
            }

            ApplyRegistrySnapshot();

            if (_settingsService != null
                || _hotkeyConfigService != null
                || _shortcutService != null
                || _scheduledTaskRepository != null
                || _textExpansionStorageService != null)
            {
                await ReloadProfileServicesAsync(GetProfileDirectory(_registry.ActiveProfile)).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize profile manager");
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SwitchProfileAsync(string profileId)
    {
        ProfileInfo activeProfile;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var previousProfileId = _registry.ActiveProfile;
            var profile = FindProfile(profileId)
                ?? throw new InvalidOperationException($"Profile '{profileId}' does not exist.");

            if (string.Equals(profile.Id, _registry.ActiveProfile, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var profileDir = GetProfileDirectory(profile.Id);

            // 1. Capture running states before stopping
            var hotkeyWasRunning = _hotkeyService?.IsRunning ?? false;
            var shortcutWasListening = _shortcutService?.IsListening ?? false;
            var schedulerWasRunning = _schedulerService?.IsRunning ?? false;
            var textExpansionWasRunning = _textExpansionService?.IsRunning ?? false;

            // 2. Stop runtime services (reverse dependency order)
            StopRuntimeServices();

            try
            {
                // 3. Reload all profile-backed storage services
                await ReloadProfileServicesAsync(profileDir).ConfigureAwait(false);

                // 4. Update registry
                _registry.ActiveProfile = profile.Id;
                await SaveRegistryAsync().ConfigureAwait(false);
                ApplyRegistrySnapshot();
                activeProfile = ActiveProfile;
            }
            catch
            {
                _registry.ActiveProfile = previousProfileId;
                await ReloadProfileServicesAsync(GetProfileDirectory(previousProfileId)).ConfigureAwait(false);
                RestartRuntimeServices(
                    hotkeyWasRunning,
                    shortcutWasListening,
                    schedulerWasRunning,
                    textExpansionWasRunning);
                throw;
            }

            // 5. Restart services that were running and are still eligible
            RestartRuntimeServices(
                hotkeyWasRunning,
                shortcutWasListening,
                schedulerWasRunning,
                textExpansionWasRunning);

            Log.Information("Switched active profile to {ProfileId}", profile.Id);
        }
        finally
        {
            _gate.Release();
        }

        ProfileChanged?.Invoke(this, activeProfile);
    }

    private void StopRuntimeServices()
    {
        try { _textExpansionService?.Stop(); }
        catch (Exception ex) { Log.Warning(ex, "Failed to stop text expansion service"); }

        try { _schedulerService?.Stop(); }
        catch (Exception ex) { Log.Warning(ex, "Failed to stop scheduler service"); }

        try { _shortcutService?.Stop(); }
        catch (Exception ex) { Log.Warning(ex, "Failed to stop shortcut service"); }

        try { _hotkeyService?.Stop(); }
        catch (Exception ex) { Log.Warning(ex, "Failed to stop hotkey service"); }
    }

    private async Task ReloadProfileServicesAsync(string profileDir)
    {
        if (_settingsService != null)
        {
            await _settingsService.ReloadAsync(profileDir).ConfigureAwait(false);
        }

        if (_hotkeyConfigService != null)
        {
            await _hotkeyConfigService.ReloadAsync(profileDir).ConfigureAwait(false);

            // Mutate the singleton HotkeySettings object with values from the new profile
            if (_hotkeySettings != null)
            {
                var loaded = _hotkeyConfigService.Load();
                _hotkeySettings.RecordingHotkey = loaded.RecordingHotkey;
                _hotkeySettings.PlaybackHotkey = loaded.PlaybackHotkey;
                _hotkeySettings.PauseHotkey = loaded.PauseHotkey;

                _hotkeyService?.ApplyHotkeys(
                    _hotkeySettings.RecordingHotkey,
                    _hotkeySettings.PlaybackHotkey,
                    _hotkeySettings.PauseHotkey);
            }
        }

        if (_shortcutService != null)
        {
            await _shortcutService.ReloadAsync(profileDir).ConfigureAwait(false);
        }

        if (_scheduledTaskRepository != null)
        {
            await _scheduledTaskRepository.ReloadAsync(profileDir).ConfigureAwait(false);
        }

        if (_schedulerService != null)
        {
            await _schedulerService.LoadAsync().ConfigureAwait(false);
        }

        if (_textExpansionStorageService != null)
        {
            await _textExpansionStorageService.ReloadAsync(profileDir).ConfigureAwait(false);
        }
    }

    private void RestartRuntimeServices(
        bool hotkeyWasRunning,
        bool shortcutWasListening,
        bool schedulerWasRunning,
        bool textExpansionWasRunning)
    {
        if (hotkeyWasRunning && _hotkeyService != null)
        {
            try
            {
                _hotkeyService.Start();
                if (_hotkeySettings != null)
                {
                    _hotkeyService.ApplyHotkeys(
                        _hotkeySettings.RecordingHotkey,
                        _hotkeySettings.PlaybackHotkey,
                        _hotkeySettings.PauseHotkey);
                }
            }
            catch (Exception ex) { Log.Warning(ex, "Failed to restart hotkey service after profile switch"); }
        }

        if (shortcutWasListening && _shortcutService != null)
        {
            try { _shortcutService.Start(); }
            catch (Exception ex) { Log.Warning(ex, "Failed to restart shortcut service after profile switch"); }
        }

        if (schedulerWasRunning && _schedulerService != null)
        {
            try { _schedulerService.Start(); }
            catch (Exception ex) { Log.Warning(ex, "Failed to restart scheduler after profile switch"); }
        }

        // Restart text expansion only if it was running AND the new profile has it enabled
        if (textExpansionWasRunning && _textExpansionService != null
            && (_settingsService?.Current.EnableTextExpansion ?? false))
        {
            try { _textExpansionService.Start(); }
            catch (Exception ex) { Log.Warning(ex, "Failed to restart text expansion after profile switch"); }
        }
    }

    public async Task<ProfileInfo> CreateProfileAsync(string displayName)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateDisplayName(displayName, nameof(displayName));

            if (_registry.Profiles.Any(profile => string.Equals(profile.Name, displayName.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A profile named '{displayName.Trim()}' already exists.");
            }

            var profile = new ProfileInfo
            {
                Id = GenerateSlug(displayName),
                Name = displayName.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await CreateProfileFilesAsync(profile.Id).ConfigureAwait(false);
            _registry.Profiles.Add(profile);
            await SaveRegistryAsync().ConfigureAwait(false);
            ApplyRegistrySnapshot();

            Log.Information("Created profile {ProfileId}", profile.Id);
            return profile;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RenameProfileAsync(string profileId, string newDisplayName)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateDisplayName(newDisplayName, nameof(newDisplayName));

            var profile = FindProfile(profileId)
                ?? throw new InvalidOperationException($"Profile '{profileId}' does not exist.");
            var trimmedName = newDisplayName.Trim();

            if (_registry.Profiles.Any(candidate =>
                    !string.Equals(candidate.Id, profile.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A profile named '{trimmedName}' already exists.");
            }

            profile.Name = trimmedName;
            await SaveRegistryAsync().ConfigureAwait(false);
            ApplyRegistrySnapshot();

            Log.Information("Renamed profile {ProfileId} to {ProfileName}", profile.Id, profile.Name);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (string.Equals(profileId, DefaultProfileId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The default profile cannot be deleted.");
            }

            if (string.Equals(profileId, _registry.ActiveProfile, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The active profile cannot be deleted.");
            }

            var profile = FindProfile(profileId)
                ?? throw new InvalidOperationException($"Profile '{profileId}' does not exist.");

            var profileDirectory = GetProfileDirectory(profile.Id);
            _registry.Profiles.Remove(profile);
            await SaveRegistryAsync().ConfigureAwait(false);
            ApplyRegistrySnapshot();

            if (Directory.Exists(profileDirectory))
            {
                Directory.Delete(profileDirectory, recursive: true);
            }

            Log.Information("Deleted profile {ProfileId}", profile.Id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public string GetProfileDirectory(string profileId)
    {
        return Path.Combine(_profilesRootPath, profileId);
    }

    private async Task<ProfileRegistry> LoadRegistryAsync()
    {
        var registry = await FileBackedJsonStorage.ReadAsync(_registryFilePath, CrossMacroJsonContext.Default.ProfileRegistry)
            .ConfigureAwait(false);

        return registry ?? new ProfileRegistry();
    }

    private async Task<ProfileRegistry> MigrateFlatConfigurationAsync()
    {
        var defaultProfileDirectory = GetProfileDirectory(DefaultProfileId);
        Directory.CreateDirectory(defaultProfileDirectory);

        var oldSettings = await FileBackedJsonStorage.ReadAsync(
                GetRootConfigPath(ConfigFileNames.Settings),
                CrossMacroJsonContext.Default.AppSettings)
            .ConfigureAwait(false)
            ?? new AppSettings();

        await FileBackedJsonStorage.WriteAsync(
                GetRootConfigPath(ConfigFileNames.GlobalSettings),
                SettingsMapper.ToGlobal(oldSettings),
                CrossMacroJsonContext.Default.GlobalSettings)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(defaultProfileDirectory, ConfigFileNames.Settings),
                SettingsMapper.ToProfile(oldSettings),
                CrossMacroJsonContext.Default.ProfileSettings)
            .ConfigureAwait(false);

        await CopyExistingProfileConfigFilesAsync(defaultProfileDirectory).ConfigureAwait(false);

        return CreateDefaultRegistry();
    }

    private async Task<ProfileRegistry> CreateFreshDefaultProfileAsync()
    {
        await EnsureDefaultProfileDirectoryAsync().ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                GetRootConfigPath(ConfigFileNames.GlobalSettings),
                new GlobalSettings(),
                CrossMacroJsonContext.Default.GlobalSettings)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(GetProfileDirectory(DefaultProfileId), ConfigFileNames.Settings),
                new ProfileSettings(),
                CrossMacroJsonContext.Default.ProfileSettings)
            .ConfigureAwait(false);

        return CreateDefaultRegistry();
    }

    private async Task EnsureDefaultProfileDirectoryAsync()
    {
        var defaultProfileDirectory = GetProfileDirectory(DefaultProfileId);
        Directory.CreateDirectory(defaultProfileDirectory);

        var defaultSettingsPath = Path.Combine(defaultProfileDirectory, ConfigFileNames.Settings);
        if (!File.Exists(defaultSettingsPath))
        {
            await FileBackedJsonStorage.WriteAsync(
                    defaultSettingsPath,
                    new ProfileSettings(),
                    CrossMacroJsonContext.Default.ProfileSettings)
                .ConfigureAwait(false);
        }
    }

    private async Task CreateProfileFilesAsync(string profileId)
    {
        var profileDirectory = GetProfileDirectory(profileId);
        Directory.CreateDirectory(profileDirectory);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(profileDirectory, ConfigFileNames.Settings),
                new ProfileSettings(),
                CrossMacroJsonContext.Default.ProfileSettings)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(profileDirectory, ConfigFileNames.Hotkeys),
                new HotkeySettings(),
                CrossMacroJsonContext.Default.HotkeySettings)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(profileDirectory, ConfigFileNames.Shortcuts),
                new List<ShortcutTask>(),
                CrossMacroJsonContext.Default.ListShortcutTask)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(profileDirectory, ConfigFileNames.Schedules),
                new List<ScheduledTask>(),
                CrossMacroJsonContext.Default.ListScheduledTask)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(profileDirectory, ConfigFileNames.TextExpansions),
                new List<global::CrossMacro.Core.Models.TextExpansion>(),
                CrossMacroJsonContext.Default.ListTextExpansion)
            .ConfigureAwait(false);
    }

    private Task CopyExistingProfileConfigFilesAsync(string profileDirectory)
    {
        foreach (var fileName in MigratedProfileConfigFiles)
        {
            var sourcePath = GetRootConfigPath(fileName);
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, Path.Combine(profileDirectory, fileName), overwrite: true);
            }
        }

        return Task.CompletedTask;
    }

    private async Task SaveRegistryAsync()
    {
        await FileBackedJsonStorage.WriteAsync(_registryFilePath, _registry, CrossMacroJsonContext.Default.ProfileRegistry)
            .ConfigureAwait(false);
    }

    private void NormalizeRegistry()
    {
        if (_registry.Profiles.Count == 0)
        {
            _registry.Profiles.Add(CreateDefaultProfileInfo());
        }

        if (_registry.Profiles.All(profile => !string.Equals(profile.Id, DefaultProfileId, StringComparison.OrdinalIgnoreCase)))
        {
            _registry.Profiles.Insert(0, CreateDefaultProfileInfo());
        }

        if (string.IsNullOrWhiteSpace(_registry.ActiveProfile)
            || _registry.Profiles.All(profile => !string.Equals(profile.Id, _registry.ActiveProfile, StringComparison.OrdinalIgnoreCase)))
        {
            _registry.ActiveProfile = DefaultProfileId;
        }
    }

    private void ApplyRegistrySnapshot()
    {
        Profiles = _registry.Profiles
            .Select(profile => new ProfileInfo
            {
                Id = profile.Id,
                Name = profile.Name,
                CreatedAt = profile.CreatedAt
            })
            .ToList();

        ActiveProfile = Profiles.FirstOrDefault(profile => string.Equals(profile.Id, _registry.ActiveProfile, StringComparison.OrdinalIgnoreCase))
            ?? Profiles.First(profile => string.Equals(profile.Id, DefaultProfileId, StringComparison.OrdinalIgnoreCase));
    }

    private ProfileInfo? FindProfile(string profileId)
    {
        return _registry.Profiles.FirstOrDefault(profile => string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
    }

    private string GenerateSlug(string displayName)
    {
        var builder = new StringBuilder(displayName.Length);
        var previousWasHyphen = false;

        foreach (var character in displayName.ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
                previousWasHyphen = false;
            }
            else if (!previousWasHyphen)
            {
                builder.Append('-');
                previousWasHyphen = true;
            }
        }

        var baseSlug = builder.ToString().Trim('-');
        if (baseSlug.Length == 0)
        {
            baseSlug = "profile";
        }

        var slug = baseSlug;
        var suffix = 2;
        while (_registry.Profiles.Any(profile => string.Equals(profile.Id, slug, StringComparison.OrdinalIgnoreCase)))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }

    private static void ValidateDisplayName(string displayName, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException($"{parameterName} cannot be empty.");
        }

        if (displayName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new InvalidOperationException($"{parameterName} cannot contain path separators.");
        }
    }

    private string GetRootConfigPath(string fileName)
    {
        return Path.Combine(_configRootPath, fileName);
    }

    private static ProfileRegistry CreateDefaultRegistry()
    {
        return new ProfileRegistry
        {
            Version = 1,
            ActiveProfile = DefaultProfileId,
            Profiles = [CreateDefaultProfileInfo()]
        };
    }

    private static ProfileInfo CreateDefaultProfileInfo()
    {
        return new ProfileInfo
        {
            Id = DefaultProfileId,
            Name = DefaultProfileName,
            CreatedAt = DateTime.UtcNow
        };
    }
}
