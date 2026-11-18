using CrossMacro.Application.Settings;
namespace CrossMacro.Commands.Settings;

public sealed class SettingsCommandService(
    ISettingsService settingsService,
    SettingsChangeCoordinator settingsChanges,
    IPortalScreenCastRestoreStateService? portalRestoreStateService = null) : ISettingsCommandService
{
    private const string PortalRestoreStateKey = SettingsDescriptorCatalog.PortalRestoreStateKey;

    private readonly ISettingsService _settingsService = settingsService;
    private readonly SettingsChangeCoordinator _settingsChanges = settingsChanges;
    private readonly IPortalScreenCastRestoreStateService? _portalRestoreStateService = portalRestoreStateService;

    public static IReadOnlyList<string> SupportedKeys => SettingsDescriptorCatalog.Keys;

    /// <summary>True for keys whose value must never be shown to a remote client.</summary>
    public static bool IsSensitive(string key) => SettingsDescriptorCatalog.IsSensitive(key);

    public async Task<OperationResult> GetAsync(string? key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(key, PortalRestoreStateKey, StringComparison.Ordinal))
        {
            return await GetPortalRestoreStateAsync(cancellationToken).ConfigureAwait(false);
        }

        _ = await _settingsService.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var settings = _settingsService.AccessCurrent(AppSettingsSnapshot.Copy);

        if (string.IsNullOrWhiteSpace(key))
        {
            return new OperationResult
            {
                Success = true,
                Outcome = ExecutionOutcomeCode.Success,
                Message = "Settings loaded.",
                Data = await BuildSettingsDictionaryAsync(settings, cancellationToken).ConfigureAwait(false),
            };
        }

        if (!SettingsDescriptorCatalog.TryGet(key, out var descriptor))
        {
            return UnknownKey(key);
        }

        var value = descriptor.Read(settings);
        return new OperationResult
        {
            Success = true,
            Outcome = ExecutionOutcomeCode.Success,
            Message = $"{key}={value}",
            Data = new SettingsValueData(key, value),
        };
    }

    public async Task<OperationResult> SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            return MissingKey();
        }

        if (string.Equals(key, PortalRestoreStateKey, StringComparison.Ordinal))
        {
            return Failure(
                "Invalid settings value.",
                $"{PortalRestoreStateKey} is status-only; use settings reset {PortalRestoreStateKey} to clear it.");
        }

        return await MutateAsync(
            key,
            "Invalid settings value.",
            $"{key} updated.",
            (descriptor, settings) =>
            {
                var written = descriptor.Write(settings, value);
                return written.Success ? null : written.Error;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<OperationResult> ListKeysAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new OperationResult
        {
            Success = true,
            Outcome = ExecutionOutcomeCode.Success,
            Message = "Supported settings keys loaded.",
            Data = SupportedKeys.ToList(),
        });
    }

    public async Task<OperationResult> ResetAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            return MissingKey();
        }

        if (string.Equals(key, PortalRestoreStateKey, StringComparison.Ordinal))
        {
            return await ResetPortalRestoreStateAsync(cancellationToken).ConfigureAwait(false);
        }

        var defaults = new AppSettings();
        return await MutateAsync(
            key,
            "Settings key cannot be reset.",
            $"{key} reset.",
            (descriptor, settings) =>
            {
                descriptor.Reset(settings, defaults);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The shared set/reset flow: resolve the key, apply the change to a copy, then commit it with rollback.
    /// <paramref name="apply"/> returns an error message, or null when the change was applied.
    /// </summary>
    private async Task<OperationResult> MutateAsync(
        string key,
        string rejectedMessage,
        string successMessage,
        Func<SettingsDescriptor, AppSettings, string?> apply,
        CancellationToken cancellationToken)
    {
        _ = await _settingsService.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var settings = _settingsService.AccessCurrent(AppSettingsSnapshot.Copy);

        if (!SettingsDescriptorCatalog.TryGet(key, out var descriptor))
        {
            return UnknownKey(key);
        }

        var beforeValue = descriptor.Read(settings);
        var before = AppSettingsSnapshot.Copy(settings);
        var after = AppSettingsSnapshot.Copy(settings);
        var error = apply(descriptor, after);
        if (error is not null)
        {
            return Failure(rejectedMessage, error);
        }

        try
        {
            after.Normalize();
            await _settingsChanges.CommitAsync(new SettingsChangeRequest(before, after), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new OperationResult
            {
                Success = false,
                Outcome = ExecutionOutcomeCode.RuntimeError,
                Message = "Failed to save settings.",
                Errors = [ex.Message],
            };
        }

        return new OperationResult
        {
            Success = true,
            Outcome = ExecutionOutcomeCode.Success,
            Message = successMessage,
            Data = new SettingsMutationData(key, beforeValue, descriptor.Read(after)),
        };
    }

    private static OperationResult MissingKey() => new()
    {
        Success = false,
        Outcome = ExecutionOutcomeCode.InvalidArguments,
        Message = "Missing settings key.",
    };

    private static OperationResult UnknownKey(string key) =>
        Failure("Unknown settings key.", $"Unknown key: {key}", $"Available keys: {string.Join(", ", SupportedKeys)}");

    private static OperationResult Failure(string message, params string[] errors) => new()
    {
        Success = false,
        Outcome = ExecutionOutcomeCode.InvalidArguments,
        Message = message,
        Errors = errors,
    };

    private async Task<Dictionary<string, object?>> BuildSettingsDictionaryAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var values = SettingsDescriptorCatalog.Values.ToDictionary(descriptor => descriptor.Key, descriptor => descriptor.Read(settings), StringComparer.Ordinal);
        values[PortalRestoreStateKey] = await GetPortalRestoreStateValueAsync(cancellationToken).ConfigureAwait(false);
        return values;
    }

    private async Task<OperationResult> GetPortalRestoreStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var value = await GetPortalRestoreStateValueAsync(cancellationToken).ConfigureAwait(false);
            return new OperationResult
            {
                Success = true,
                Outcome = ExecutionOutcomeCode.Success,
                Message = $"{PortalRestoreStateKey}={value}",
                Data = new SettingsValueData(PortalRestoreStateKey, value),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new OperationResult
            {
                Success = false,
                Outcome = ExecutionOutcomeCode.RuntimeError,
                Message = "Failed to read Portal restore state.",
                Errors = [ex.Message],
            };
        }
    }

    private async Task<OperationResult> ResetPortalRestoreStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var beforeValue = await GetPortalRestoreStateValueAsync(cancellationToken).ConfigureAwait(false);
            if (_portalRestoreStateService is not null)
            {
                await _portalRestoreStateService.ClearRestoreStateAsync(cancellationToken).ConfigureAwait(false);
            }

            var afterValue = await GetPortalRestoreStateValueAsync(cancellationToken).ConfigureAwait(false);
            return new OperationResult
            {
                Success = true,
                Outcome = ExecutionOutcomeCode.Success,
                Message = $"{PortalRestoreStateKey} reset.",
                Data = new SettingsMutationData(PortalRestoreStateKey, beforeValue, afterValue),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new OperationResult
            {
                Success = false,
                Outcome = ExecutionOutcomeCode.RuntimeError,
                Message = "Failed to reset Portal restore state.",
                Errors = [ex.Message],
            };
        }
    }

    private async Task<string> GetPortalRestoreStateValueAsync(CancellationToken cancellationToken) =>
        _portalRestoreStateService is not null && await _portalRestoreStateService.HasRestoreStateAsync(cancellationToken).ConfigureAwait(false)
            ? "set"
            : "empty";
}
