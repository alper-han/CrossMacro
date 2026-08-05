
using CrossMacro.Infrastructure.Persistence.Settings;

namespace CrossMacro.Infrastructure.Services;

internal class ProfileManager : IProfileCatalog
{
    private const string DefaultProfileId = "default";
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
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposed;

    private ProfileRegistry _registry = new();

    public ProfileInfo ActiveProfile { get; private set; } = new();

    public IReadOnlyList<ProfileInfo> Profiles { get; private set; } = [];

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

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateRootAncestors(_configRootPath, "Configuration root");
            ValidateRootAncestors(_profilesRootPath, "Profiles root");
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

    public void RestoreActiveProfile(string profileId)
    {
        _registry.ActiveProfile = profileId;
        ApplyRegistrySnapshot();
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
                CreatedAt = DateTime.UtcNow,
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
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) is 0)
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

        var current = new DirectoryInfo(profileDirectory);
        while (current is not null)
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException($"Profile path for '{profileId}' must not contain a reparse point.");
            }

            current = current.Parent;
        }

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

    private static void ValidateRootAncestors(string path, string description)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current is not null)
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException($"{description} path must not contain a reparse point.");
            }

            current = current.Parent;
        }
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
        _ = Directory.CreateDirectory(defaultProfileDirectory);

        var oldSettings = await FileBackedJsonStorage.ReadAsync(
                GetRootConfigPath(ConfigFileNames.Settings),
                CrossMacroJsonContext.Default.AppSettings)
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

        await CopyExistingProfileConfigFilesAsync(defaultProfileDirectory).ConfigureAwait(false);

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
                Path.Combine(GetProfileDirectory(DefaultProfileId), ConfigFileNames.Settings),
                new PersistedProfileSettings(),
                CrossMacroJsonContext.Default.PersistedProfileSettings,
                CancellationToken.None)
            .ConfigureAwait(false);

        return CreateDefaultRegistry();
    }

    private async Task EnsureDefaultProfileDirectoryAsync()
    {
        var defaultProfileDirectory = GetProfileDirectory(DefaultProfileId);
        _ = Directory.CreateDirectory(defaultProfileDirectory);

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

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(profileDirectory, ConfigFileNames.Settings),
                new PersistedProfileSettings(),
                CrossMacroJsonContext.Default.PersistedProfileSettings,
                CancellationToken.None)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(profileDirectory, ConfigFileNames.Hotkeys),
                new HotkeySettings(),
                CrossMacroJsonContext.Default.HotkeySettings,
                CancellationToken.None)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(profileDirectory, ConfigFileNames.Shortcuts),
                new List<ShortcutTask>(),
                CrossMacroJsonContext.Default.ListShortcutTask,
                CancellationToken.None)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(profileDirectory, ConfigFileNames.Schedules),
                new List<ScheduledTask>(),
                CrossMacroJsonContext.Default.ListScheduledTask,
                CancellationToken.None)
            .ConfigureAwait(false);

        await FileBackedJsonStorage.WriteAsync(
                Path.Combine(profileDirectory, ConfigFileNames.TextExpansions),
                new List<global::CrossMacro.Core.Models.TextExpansionEntry>(),
                CrossMacroJsonContext.Default.ListTextExpansionEntry,
                CancellationToken.None)
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
                CreatedAt = profile.CreatedAt,
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

    private static ProfileRegistry CreateDefaultRegistry()
    {
        var registry = new ProfileRegistry
        {
            Version = 1,
            ActiveProfile = DefaultProfileId,
        };

        registry.ReplaceProfiles([CreateDefaultProfileInfo()]);
        return registry;
    }

    private static ProfileInfo CreateDefaultProfileInfo()
    {
        return new ProfileInfo
        {
            Id = DefaultProfileId,
            Name = DefaultProfileName,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
