using CrossMacro.Core.Models.Profiles;
using CrossMacro.Infrastructure.Persistence.Macros;
using CrossMacro.Infrastructure.Persistence.Settings;

namespace CrossMacro.Infrastructure.Services.Profiles;

internal sealed class ProfileCatalog : IProfileCatalog
{
    private const string DefaultProfileName = "Default";

    private static readonly string[] MigratedProfileConfigFiles =
    [
        ConfigFileNames.Hotkeys,
        ConfigFileNames.Shortcuts,
        ConfigFileNames.Schedules,
        ConfigFileNames.TextExpansions,
        ConfigFileNames.Triggers,
    ];

    private readonly string _configRootPath;
    private readonly string _profilesRootPath;
    private readonly string _registryFilePath;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposed;

    private ProfileRegistry _registry = new();

    public ProfileInfo ActiveProfile { get; private set; } = new();

    public IReadOnlyList<ProfileInfo> Profiles { get; private set; } = [];

        public ProfileCatalog(string configRootPath, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configRootPath);
        _configRootPath = configRootPath;

        _profilesRootPath = Path.Combine(_configRootPath, ConfigFileNames.ProfilesDirectory);
        _registryFilePath = Path.Combine(_configRootPath, ConfigFileNames.ProfileRegistry);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _ = Directory.CreateDirectory(_configRootPath);
            _ = Directory.CreateDirectory(_profilesRootPath);

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

        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to initialize profile manager");
            throw;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async Task SetActiveProfileAsync(string profileId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var profile = FindProfile(profileId)
                ?? throw new InvalidOperationException($"Profile '{profileId}' does not exist.");
            _registry.ActiveProfile = profile.Id;
            await SaveRegistryAsync().ConfigureAwait(false);
            ApplyRegistrySnapshot();
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async Task RestoreActiveProfileAsync(string profileId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _registry.ActiveProfile = profileId;
            ApplyRegistrySnapshot();
        }
        finally
        {
            _ = _gate.Release();
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
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
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
            _ = _gate.Release();
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
            _ = _gate.Release();
        }
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (string.Equals(profileId, ConfigFileNames.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
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
            _ = _registry.Profiles.Remove(profile);
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
            _ = _gate.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is 0)
        {
            _gate.Dispose();
        }
    }

    public string GetProfileDirectory(string profileId)
    {
        ValidateProfileId(profileId);

        var profilesRoot = Path.GetFullPath(_profilesRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var profileDirectory = Path.GetFullPath(Path.Combine(profilesRoot, profileId));
        if (!profileDirectory.StartsWith(profilesRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Profile id '{profileId}' resolves outside the profiles directory.");
        }

        ValidateProfileDirectory(profileDirectory, profileId);
        return profileDirectory;
    }

    private static void ValidateProfileId(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw new InvalidDataException("Profile id cannot be empty.");
        }

        for (var index = 0; index < profileId.Length; index++)
        {
            var character = profileId[index];
            var valid = character is (>= 'a' and <= 'z')
                or (>= 'A' and <= 'Z')
                or (>= '0' and <= '9')
                or '-';
            if (!valid || (index is 0 && character == '-') || (index == profileId.Length - 1 && character == '-'))
            {
                throw new InvalidDataException($"Profile id '{profileId}' contains unsupported path characters.");
            }
        }
    }

    private static void ValidateProfileDirectory(string profileDirectory, string profileId)
    {
        var directory = new DirectoryInfo(profileDirectory);
        if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException($"Profile path for '{profileId}' must not be a reparse point.");
        }
    }

    private async Task<ProfileRegistry> LoadRegistryAsync()
    {
        var registry = await FileBackedJsonStorage.ReadAsync(_registryFilePath, CrossMacroJsonContext.Default.ProfileRegistry, CancellationToken.None)
            .ConfigureAwait(false);

        return registry ?? new ProfileRegistry();
    }

    private async Task<ProfileRegistry> MigrateFlatConfigurationAsync()
    {
        var defaultProfileDirectory = GetProfileDirectory(ConfigFileNames.DefaultProfileId);
        _ = Directory.CreateDirectory(defaultProfileDirectory);
        _ = Directory.CreateDirectory(Path.Combine(defaultProfileDirectory, ConfigFileNames.MacrosDirectory));

        var oldSettings = await FileBackedJsonStorage.ReadAsync(
                GetRootConfigPath(ConfigFileNames.Settings),
                CrossMacroJsonContext.Default.AppSettings,
                CancellationToken.None)
            .ConfigureAwait(false)
            ?? new AppSettings();

        await FileBackedJsonStorage.WriteAsync(
                GetRootConfigPath(ConfigFileNames.GlobalSettings),
                SettingsPersistenceMapper.ToGlobal(oldSettings),
                CrossMacroJsonContext.Default.PersistedGlobalSettings,
                CancellationToken.None)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(defaultProfileDirectory, ConfigFileNames.Settings),
                SettingsPersistenceMapper.ToProfile(oldSettings),
                CrossMacroJsonContext.Default.PersistedProfileSettings,
                CancellationToken.None)
            .ConfigureAwait(false);

        CopyExistingProfileConfigFiles(defaultProfileDirectory);

        return CreateDefaultRegistry();
    }

    private async Task<ProfileRegistry> CreateFreshDefaultProfileAsync()
    {
        await EnsureDefaultProfileDirectoryAsync().ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                GetRootConfigPath(ConfigFileNames.GlobalSettings),
                new PersistedGlobalSettings(),
                CrossMacroJsonContext.Default.PersistedGlobalSettings,
                CancellationToken.None)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(GetProfileDirectory(ConfigFileNames.DefaultProfileId), ConfigFileNames.Settings),
                new PersistedProfileSettings(),
                CrossMacroJsonContext.Default.PersistedProfileSettings,
                CancellationToken.None)
            .ConfigureAwait(false);

        return CreateDefaultRegistry();
    }

    private async Task EnsureDefaultProfileDirectoryAsync()
    {
        var defaultProfileDirectory = GetProfileDirectory(ConfigFileNames.DefaultProfileId);
        _ = Directory.CreateDirectory(defaultProfileDirectory);
        _ = Directory.CreateDirectory(Path.Combine(defaultProfileDirectory, ConfigFileNames.MacrosDirectory));

        var defaultSettingsPath = Path.Combine(defaultProfileDirectory, ConfigFileNames.Settings);
        if (!File.Exists(defaultSettingsPath))
        {
            await FileBackedJsonStorage.WriteAsync(
                    defaultSettingsPath,
                    new PersistedProfileSettings(),
                    CrossMacroJsonContext.Default.PersistedProfileSettings,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private async Task CreateProfileFilesAsync(string profileId)
    {
        var profileDirectory = GetProfileDirectory(profileId);
        _ = Directory.CreateDirectory(profileDirectory);
        _ = Directory.CreateDirectory(Path.Combine(profileDirectory, ConfigFileNames.MacrosDirectory));

        await WriteInitialFileAsync(profileDirectory, ConfigFileNames.Settings, new PersistedProfileSettings(), CrossMacroJsonContext.Default.PersistedProfileSettings).ConfigureAwait(false);
        await WriteInitialFileAsync(profileDirectory, ConfigFileNames.Hotkeys, new HotkeySettings(), CrossMacroJsonContext.Default.HotkeySettings).ConfigureAwait(false);
        await WriteInitialFileAsync(profileDirectory, ConfigFileNames.Shortcuts, new List<ShortcutTask>(), CrossMacroJsonContext.Default.ListShortcutTask).ConfigureAwait(false);
        await WriteInitialFileAsync(profileDirectory, ConfigFileNames.Triggers, new List<TriggerTask>(), CrossMacroJsonContext.Default.ListTriggerTask).ConfigureAwait(false);
        await WriteInitialFileAsync(profileDirectory, ConfigFileNames.Schedules, new List<ScheduledTask>(), CrossMacroJsonContext.Default.ListScheduledTask).ConfigureAwait(false);
        await WriteInitialFileAsync(profileDirectory, ConfigFileNames.TextExpansions, new List<TextExpansionEntry>(), CrossMacroJsonContext.Default.ListTextExpansionEntry).ConfigureAwait(false);
        await WriteInitialFileAsync(profileDirectory, ConfigFileNames.LoadedMacros, new PersistedLoadedMacroSession(), CrossMacroJsonContext.Default.PersistedLoadedMacroSession).ConfigureAwait(false);
    }

    private static Task WriteInitialFileAsync<T>(string profileDirectory, string fileName, T value, JsonTypeInfo<T> typeInfo) =>
        FileBackedJsonStorage.WriteAsync(Path.Combine(profileDirectory, fileName), value, typeInfo, CancellationToken.None);

    private void CopyExistingProfileConfigFiles(string profileDirectory)
    {
        foreach (var fileName in MigratedProfileConfigFiles)
        {
            var sourcePath = GetRootConfigPath(fileName);
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, Path.Combine(profileDirectory, fileName), overwrite: true);
            }
        }
    }

    private async Task SaveRegistryAsync()
    {
        await FileBackedJsonStorage.WriteAsync(_registryFilePath, _registry, CrossMacroJsonContext.Default.ProfileRegistry, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private void NormalizeRegistry()
    {
        foreach (var profile in _registry.Profiles)
        {
            ValidateProfileId(profile.Id);
        }

        if (!string.IsNullOrWhiteSpace(_registry.ActiveProfile))
        {
            ValidateProfileId(_registry.ActiveProfile);
        }

        if (_registry.Profiles.Count is 0)
        {
            _registry.Profiles.Add(CreateDefaultProfileInfo());
        }

        if (_registry.Profiles.All(profile => !string.Equals(profile.Id, ConfigFileNames.DefaultProfileId, StringComparison.OrdinalIgnoreCase)))
        {
            _registry.Profiles.Insert(0, CreateDefaultProfileInfo());
        }

        if (string.IsNullOrWhiteSpace(_registry.ActiveProfile)
            || _registry.Profiles.All(profile => !string.Equals(profile.Id, _registry.ActiveProfile, StringComparison.OrdinalIgnoreCase)))
        {
            _registry.ActiveProfile = ConfigFileNames.DefaultProfileId;
        }
    }

    private void ApplyRegistrySnapshot()
    {
        Profiles = _registry.Profiles
            .Select(profile => new ProfileInfo
            {
                Id = profile.Id,
                Name = profile.Name,
                CreatedAt = profile.CreatedAt,
            })
            .ToList();

        ActiveProfile = Profiles.FirstOrDefault(profile => string.Equals(profile.Id, _registry.ActiveProfile, StringComparison.OrdinalIgnoreCase))
            ?? Profiles.First(profile => string.Equals(profile.Id, ConfigFileNames.DefaultProfileId, StringComparison.OrdinalIgnoreCase));
    }

    private ProfileInfo? FindProfile(string profileId)
    {
        return _registry.Profiles.FirstOrDefault(profile => string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
    }

    private string GenerateSlug(string displayName)
    {
        var builder = new StringBuilder(displayName.Length);
        var previousWasHyphen = false;

        foreach (var character in displayName)
        {
            var normalizedCharacter = char.ToLowerInvariant(character);
            if (normalizedCharacter is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                _ = builder.Append(normalizedCharacter);
                previousWasHyphen = false;
            }
            else if (!previousWasHyphen)
            {
                _ = builder.Append('-');
                previousWasHyphen = true;
            }
        }

        var baseSlug = builder.ToString().Trim('-');
        if (baseSlug.Length is 0)
        {
            baseSlug = "profile";
        }

        var slug = baseSlug;
        var suffix = 2;
        while (_registry.Profiles.Any(profile => string.Equals(profile.Id, slug, StringComparison.OrdinalIgnoreCase)))
        {
            slug = $"{baseSlug}-{suffix.ToString(CultureInfo.InvariantCulture)}";
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

    private ProfileRegistry CreateDefaultRegistry()
    {
        var registry = new ProfileRegistry
        {
            Version = 1,
            ActiveProfile = ConfigFileNames.DefaultProfileId,
        };

        registry.ReplaceProfiles([CreateDefaultProfileInfo()]);
        return registry;
    }

    private ProfileInfo CreateDefaultProfileInfo()
    {
        return new ProfileInfo
        {
            Id = ConfigFileNames.DefaultProfileId,
            Name = DefaultProfileName,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };
    }
}
