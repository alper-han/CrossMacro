namespace CrossMacro.UI.Services.Settings;

/// <summary>Submits an owned draft snapshot and restores presentation after a failed save.</summary>
internal static class SettingsDraftPersistence
{
    internal static Task SubmitAsync(
        SettingsChangeCoordinator coordinator,
        AppSettings draft,
        ref AppSettings lastSubmitted,
        Func<Task> refreshAfterFailure,
        string failureMessage)
    {
        var request = new SettingsChangeRequest(lastSubmitted, AppSettingsSnapshot.Copy(draft));
        lastSubmitted = AppSettingsSnapshot.Copy(draft);
        return PersistAsync(coordinator, request, refreshAfterFailure, failureMessage);
    }

    private static async Task PersistAsync(
        SettingsChangeCoordinator coordinator,
        SettingsChangeRequest request,
        Func<Task> refreshAfterFailure,
        string failureMessage)
    {
        try
        {
            await coordinator.CommitAsync(request, SettingsSaveMode.AfterIdle, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            await refreshAfterFailure().ConfigureAwait(false);
            Log.LogError(error, failureMessage);
        }
    }
}
