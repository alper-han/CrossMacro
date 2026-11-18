namespace CrossMacro.UI.ViewModels.Settings;

/// <summary>
/// Profile picker and profile operations (create, rename, delete, switch) of the Settings tab.
/// </summary>
public sealed partial class SettingsProfilesViewModel : ViewModelBase, IDisposable
{
    private readonly IProfileManager? _profileManager;
    private readonly IDialogService? _dialogService;
    private readonly IManageProfile? _manageProfile;
    private readonly ILocalizationService _localizationService;
    private readonly SettingsChangeCoordinator _settingsChanges;
    private bool _disposed;

    [ObservableProperty]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    private ProfileInfo? _selectedProfile;

    [ObservableProperty]
    public partial IReadOnlyList<ProfileInfo> AvailableProfiles { get; private set; }

    [ObservableProperty]
    public partial bool IsProfileOperationInProgress { get; private set; }

    public event EventHandler<string>? ProfileOperationFailed;

    /// <summary>Raised on the UI thread after the active profile changed and profile-specific settings must be re-read.</summary>
    internal event EventHandler? ProfileSettingsInvalidated;

    internal SettingsProfilesViewModel(
        IProfileManager? profileManager,
        IDialogService? dialogService,
        IManageProfile? manageProfile,
        ILocalizationService localizationService,
        SettingsChangeCoordinator settingsChanges,
        IUiDispatcher? uiDispatcher)
        : base(uiDispatcher)
    {
        _profileManager = profileManager;
        _dialogService = dialogService;
        _manageProfile = manageProfile;
        _localizationService = localizationService;
        _settingsChanges = settingsChanges;
        AvailableProfiles = [];
        RefreshProfileState();
        _profileManager?.ProfileChanged += OnProfileChanged;
    }

    public async Task CreateProfileAsync()
    {
        var profileName = NewProfileName.Trim();
        if (_manageProfile is null || profileName.Length is 0)
        {
            return;
        }

        await RunProfileOperationAsync(async () =>
        {
            var createdProfile = (await _manageProfile.CreateAsync(new ProfileRequest(DisplayName: profileName), default).ConfigureAwait(false)).Profile ?? throw new InvalidOperationException("Profile was not returned after creation.");
            await RunOnUiThreadAsync(() =>
            {
                RefreshProfileState(createdProfile.Id);
                NewProfileName = string.Empty;
            }).ConfigureAwait(false);
        }, _localizationService["Settings_ProfileCreateFailed"]).ConfigureAwait(false);
    }

    public async Task RenameSelectedProfileAsync()
    {
        var profileName = NewProfileName.Trim();
        var selectedProfile = SelectedProfile;
        if (_manageProfile is null || selectedProfile is null || profileName.Length is 0)
        {
            return;
        }

        await RunProfileOperationAsync(async () =>
        {
            _ = await _manageProfile.RenameAsync(new ProfileRequest(selectedProfile.Id, profileName), default).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                RefreshProfileState(selectedProfile.Id);
                NewProfileName = string.Empty;
            }).ConfigureAwait(false);
        }, _localizationService["Settings_ProfileRenameFailed"]).ConfigureAwait(false);
    }

    public async Task DeleteSelectedProfileAsync()
    {
        var selectedProfile = SelectedProfile;
        if (_manageProfile is null || selectedProfile is null)
        {
            return;
        }

        if (IsProfileOperationInProgress)
        {
            return;
        }

        if (_dialogService is not null)
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizationService["Settings_ProfileDeleteTitle"],
                string.Format(
                    _localizationService.CurrentCulture,
                    _localizationService["Settings_ProfileDeleteMessage"],
                    selectedProfile.Name)).ConfigureAwait(false);

            if (!confirmed)
            {
                return;
            }
        }

        await RunProfileOperationAsync(async () =>
        {
            _ = await _manageProfile.DeleteAsync(new ProfileRequest(Identifier: selectedProfile.Id), default).ConfigureAwait(false);

            await RunOnUiThreadAsync(() => RefreshProfileState()).ConfigureAwait(false);
        }, _localizationService["Settings_ProfileDeleteFailed"]).ConfigureAwait(false);
    }

    public async Task SwitchProfileAsync()
    {
        var selectedProfile = SelectedProfile;
        if (_manageProfile is null || selectedProfile is null)
        {
            return;
        }

        _settingsChanges.InvalidatePendingChanges();
        await RunProfileOperationAsync(async () =>
        {
            _ = await _manageProfile.SwitchAsync(new ProfileRequest(Identifier: selectedProfile.Id), default).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                RefreshProfileState(selectedProfile.Id);
                PostToUiThread(RaiseProfileSettingsInvalidated);
            }).ConfigureAwait(false);
        }, _localizationService["Settings_ProfileSwitchFailed"]).ConfigureAwait(false);
    }

    public void RefreshProfileState(string? selectedProfileId = null)
    {
        PostToUiThread(() => RefreshProfileStateCore(selectedProfileId));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _profileManager?.ProfileChanged -= OnProfileChanged;
    }

    private void RefreshProfileStateCore(string? selectedProfileId)
    {
        if (_profileManager is null)
        {
            AvailableProfiles = [];
            SelectedProfile = null;
            return;
        }

        AvailableProfiles = _profileManager.Profiles.ToArray();
        var effectiveSelectedProfileId = selectedProfileId ?? _profileManager.ActiveProfile.Id;
        SelectedProfile = AvailableProfiles.FirstOrDefault(profile =>
                              string.Equals(profile.Id, effectiveSelectedProfileId, StringComparison.Ordinal))
                          ?? _profileManager.ActiveProfile;
    }

    private void RaiseProfileSettingsInvalidated() => ProfileSettingsInvalidated?.Invoke(this, EventArgs.Empty);

    private async Task RunProfileOperationAsync(Func<Task> operation, string failureMessage)
    {
        var started = false;
        await RunOnUiThreadAsync(() =>
        {
            if (!IsProfileOperationInProgress)
            {
                IsProfileOperationInProgress = true;
                started = true;
            }
        }).ConfigureAwait(false);
        if (!started)
        {
            return;
        }

        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? failureMessage
                : $"{failureMessage}: {ex.Message}";
            Log.Warning(ex, "{FailureMessage}: {Error}", failureMessage, ex.Message);
            await RunOnUiThreadAsync(() => ProfileOperationFailed?.Invoke(this, message)).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiThreadAsync(() => IsProfileOperationInProgress = false).ConfigureAwait(false);
        }
    }

    private void OnProfileChanged(object? sender, ProfileChangedEventArgs e)
    {
        var profile = e.Profile;
        _settingsChanges.InvalidatePendingChanges();
        PostToUiThread(() =>
        {
            RefreshProfileStateCore(profile.Id);
            RaiseProfileSettingsInvalidated();
        });
    }
}
