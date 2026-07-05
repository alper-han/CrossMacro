using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class ProfileCliService : IProfileCliService
{
    private readonly IProfileManager _profileManager;

    public ProfileCliService(IProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = new ProfileListData(
            _profileManager.Profiles.Select(ToData).ToList(),
            _profileManager.ActiveProfile.Id);
        return Task.FromResult(CliCommandExecutionResult.Ok($"{data.Profiles.Count} profile(s).", data));
    }

    public Task<CliCommandExecutionResult> CurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = ToData(_profileManager.ActiveProfile);
        return Task.FromResult(CliCommandExecutionResult.Ok($"Current profile: {data.Name} ({data.Id}).", data));
    }

    public async Task<CliCommandExecutionResult> CreateAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var profile = await _profileManager.CreateProfileAsync(name).ConfigureAwait(false);
            return CliCommandExecutionResult.Ok($"Profile created: {profile.Name} ({profile.Id}).", ToData(profile));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Failed to create profile.", [ex.Message]);
        }
    }

    public async Task<CliCommandExecutionResult> SwitchAsync(string profileIdentifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolveProfile(profileIdentifier, out var profile, out var error))
        {
            return error;
        }

        try
        {
            await _profileManager.SwitchProfileAsync(profile.Id).ConfigureAwait(false);
            return CliCommandExecutionResult.Ok($"Switched to profile: {profile.Name} ({profile.Id}).", ToData(profile, isActive: true));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "Failed to switch profile.", [ex.Message]);
        }
    }

    public async Task<CliCommandExecutionResult> RenameAsync(string profileIdentifier, string newName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolveProfile(profileIdentifier, out var profile, out var error))
        {
            return error;
        }

        try
        {
            await _profileManager.RenameProfileAsync(profile.Id, newName).ConfigureAwait(false);
            var renamed = _profileManager.Profiles.FirstOrDefault(candidate => string.Equals(candidate.Id, profile.Id, StringComparison.OrdinalIgnoreCase))
                ?? new ProfileInfo { Id = profile.Id, Name = newName, CreatedAt = profile.CreatedAt };
            return CliCommandExecutionResult.Ok($"Profile renamed: {renamed.Name} ({renamed.Id}).", ToData(renamed));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Failed to rename profile.", [ex.Message]);
        }
    }

    public async Task<CliCommandExecutionResult> DeleteAsync(string profileIdentifier, bool force, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!force)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "profile delete requires --force.",
                ["Pass --force to confirm profile deletion."]);
        }

        if (!TryResolveProfile(profileIdentifier, out var profile, out var error))
        {
            return error;
        }

        try
        {
            await _profileManager.DeleteProfileAsync(profile.Id).ConfigureAwait(false);
            return CliCommandExecutionResult.Ok($"Profile deleted: {profile.Name} ({profile.Id}).", ToData(profile, isActive: false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Failed to delete profile.", [ex.Message]);
        }
    }

    private bool TryResolveProfile(
        string profileIdentifier,
        [NotNullWhen(true)] out ProfileInfo? profile,
        [NotNullWhen(false)] out CliCommandExecutionResult? result)
    {
        profile = _profileManager.Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, profileIdentifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Name, profileIdentifier, StringComparison.OrdinalIgnoreCase));

        if (profile is not null)
        {
            result = null;
            return true;
        }

        result = CliCommandExecutionResult.Fail(
            CliExitCode.InvalidArguments,
            "Profile not found.",
            [$"Unknown profile: {profileIdentifier}"]);
        return false;
    }

    private ProfileData ToData(ProfileInfo profile)
    {
        return ToData(profile, string.Equals(profile.Id, _profileManager.ActiveProfile.Id, StringComparison.OrdinalIgnoreCase));
    }

    private static ProfileData ToData(ProfileInfo profile, bool isActive)
    {
        return new ProfileData(profile.Id, profile.Name, profile.CreatedAt, isActive);
    }
}
