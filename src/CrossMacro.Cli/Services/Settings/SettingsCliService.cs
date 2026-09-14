using CrossMacro.Application.Settings;
namespace CrossMacro.Cli.Services.Settings;

public sealed class SettingsCliService(
    ISettingsService settingsService,
    IPortalScreenCastRestoreStateService? portalRestoreStateService = null,
    SettingsChangeCoordinator? settingsChanges = null) : ISettingsCliService
{
    private readonly ISettingsService _settingsService = settingsService;
    private readonly IPortalScreenCastRestoreStateService? _portalRestoreStateService = portalRestoreStateService;
    private readonly SettingsChangeCoordinator _settingsChanges = settingsChanges ?? new SettingsChangeCoordinator(settingsService);

    public async Task<SettingsCommandResult> GetAsync(string? key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(key, "screen.portalRestoreToken", StringComparison.Ordinal))
        {
            return await GetPortalRestoreStateAsync(cancellationToken).ConfigureAwait(false);
        }

        _ = await _settingsService.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var settings = _settingsService.AccessCurrent(AppSettingsSnapshot.Copy);

        if (string.IsNullOrWhiteSpace(key))
        {
            return new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Settings loaded.",
                Data = await BuildSettingsDictionaryAsync(settings, cancellationToken).ConfigureAwait(false),
            };
        }

        if (!TryGetValue(settings, key, out var value))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Unknown settings key.",
                Errors = [$"Unknown key: {key}", $"Available keys: {string.Join(", ", SupportedKeys)}"],
            };
        }

        return new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = $"{key}={value}",
            Data = new SettingsValueData(key, value),
        };
    }

    public async Task<SettingsCommandResult> SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Missing settings key.",
            };
        }

        if (value is null)
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Missing settings value.",
            };
        }

        if (string.Equals(key, "screen.portalRestoreToken", StringComparison.Ordinal))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Invalid settings value.",
                Errors = ["screen.portalRestoreToken is status-only; use settings reset screen.portalRestoreToken to clear it."],
            };
        }

        _ = await _settingsService.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var settings = _settingsService.AccessCurrent(AppSettingsSnapshot.Copy);

        if (!TryGetValue(settings, key, out var beforeValue))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Unknown settings key.",
                Errors = [$"Unknown key: {key}", $"Available keys: {string.Join(", ", SupportedKeys)}"],
            };
        }

        var before = AppSettingsSnapshot.Copy(settings);
        var after = AppSettingsSnapshot.Copy(settings);
        if (!TrySetValue(after, key, value, out var errorMessage))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Invalid settings value.",
                Errors = [errorMessage],
            };
        }

        try
        {
            after.Normalize();
            await _settingsChanges.CommitAsync(new SettingsChangeRequest(before, after), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Failed to save settings.",
                Errors = [ex.Message],
            };
        }

        _ = TryGetValue(after, key, out var afterValue);

        return new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = $"{key} updated.",
            Data = new SettingsMutationData(key, beforeValue, afterValue),
        };
    }

    public Task<SettingsCommandResult> ListKeysAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = "Supported settings keys loaded.",
            Data = SupportedKeys.ToList(),
        });
    }

    public async Task<SettingsCommandResult> ResetAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Missing settings key.",
            };
        }

        if (string.Equals(key, "screen.portalRestoreToken", StringComparison.Ordinal))
        {
            return await ResetPortalRestoreStateAsync(cancellationToken).ConfigureAwait(false);
        }

        _ = await _settingsService.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var settings = _settingsService.AccessCurrent(AppSettingsSnapshot.Copy);
        if (!TryGetValue(settings, key, out var beforeValue))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Unknown settings key.",
                Errors = [$"Unknown key: {key}", $"Available keys: {string.Join(", ", SupportedKeys)}"],
            };
        }

        var defaults = new AppSettings();
        var before = AppSettingsSnapshot.Copy(settings);
        var after = AppSettingsSnapshot.Copy(settings);
        if (!TryResetValue(after, defaults, key, out var errorMessage))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Settings key cannot be reset.",
                Errors = [errorMessage],
            };
        }

        try
        {
            after.Normalize();
            await _settingsChanges.CommitAsync(new SettingsChangeRequest(before, after), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Failed to save settings.",
                Errors = [ex.Message],
            };
        }

        _ = TryGetValue(after, key, out var afterValue);

        return new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = $"{key} reset.",
            Data = new SettingsMutationData(key, beforeValue, afterValue),
        };
    }

    public static IReadOnlyList<string> SupportedKeys => SettingsDescriptorCatalog.Keys;

    private async Task<Dictionary<string, object?>> BuildSettingsDictionaryAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var values = SettingsDescriptorCatalog.Values.ToDictionary(descriptor => descriptor.Key, descriptor => descriptor.Read(settings), StringComparer.Ordinal);
        values[SettingsDescriptorCatalog.PortalRestoreStateKey] = await GetPortalRestoreStateValueAsync(cancellationToken).ConfigureAwait(false);
        return values;
    }

    private static bool TryGetValue(AppSettings settings, string key, out object? value)
    {
        if (SettingsDescriptorCatalog.TryGet(key, out var descriptor))
        {
            value = descriptor.Read(settings);
            return true;
        }
        value = null;
        return false;
    }

    private static bool TrySetValue(AppSettings settings, string key, string rawValue, out string errorMessage)
    {
        if (SettingsDescriptorCatalog.TryGet(key, out var descriptor))
        {
            var result = descriptor.Write(settings, rawValue);
            errorMessage = result.Error;
            return result.Success;
        }
        errorMessage = $"Unknown key: {key}";
        return false;
    }

    private static bool TryResetValue(AppSettings settings, AppSettings defaults, string key, out string errorMessage)
    {
        if (SettingsDescriptorCatalog.TryGet(key, out var descriptor))
        {
            descriptor.Reset(settings, defaults);
            errorMessage = string.Empty;
            return true;
        }
        errorMessage = $"Unknown key: {key}";
        return false;
    }

    private async Task<SettingsCommandResult> GetPortalRestoreStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var value = await GetPortalRestoreStateValueAsync(cancellationToken).ConfigureAwait(false);
            return new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = $"screen.portalRestoreToken={value}",
                Data = new SettingsValueData("screen.portalRestoreToken", value),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Failed to read Portal restore state.",
                Errors = [ex.Message],
            };
        }
    }

    private async Task<SettingsCommandResult> ResetPortalRestoreStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var beforeValue = await GetPortalRestoreStateValueAsync(cancellationToken).ConfigureAwait(false);
            if (_portalRestoreStateService is not null)
            {
                await _portalRestoreStateService.ClearRestoreStateAsync(cancellationToken).ConfigureAwait(false);
            }

            var afterValue = await GetPortalRestoreStateValueAsync(cancellationToken).ConfigureAwait(false);
            return new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "screen.portalRestoreToken reset.",
                Data = new SettingsMutationData("screen.portalRestoreToken", beforeValue, afterValue),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
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
