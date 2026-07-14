using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Profiles;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class ProfileCliService : IProfileCliService
{
    private readonly IManageProfile _manageProfile;
    private readonly IProfileManager? _profileManager;

    public ProfileCliService(IManageProfile manageProfile)
    {
        _manageProfile = manageProfile;
    }

    public ProfileCliService(IProfileManager profileManager)
    {
        _profileManager = profileManager;
        _manageProfile = new ManageProfile(profileManager);
    }

    public ProfileCliService(IManageProfile manageProfile, IProfileManager profileManager)
    {
        _manageProfile = manageProfile;
        _profileManager = profileManager;
    }

    public async Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _manageProfile.ListAsync(cancellationToken).ConfigureAwait(false);
        var data = new ProfileListData(result.Profiles.Select(profile => ToData(profile, string.Equals(profile.Id, result.ActiveProfileId, StringComparison.OrdinalIgnoreCase))).ToList(), result.ActiveProfileId);
        return CliCommandExecutionResult.Ok($"{data.Profiles.Count} profile(s).", data);
    }

    public async Task<CliCommandExecutionResult> CurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _manageProfile.CurrentAsync(cancellationToken).ConfigureAwait(false);
        var data = ToData(result.Profile!, string.Equals(result.ActiveProfileId, result.Profile!.Id, StringComparison.OrdinalIgnoreCase));
        return CliCommandExecutionResult.Ok($"Current profile: {data.Name} ({data.Id}).", data);
    }

    public async Task<CliCommandExecutionResult> CreateAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var profile = (await _manageProfile.CreateAsync(new ProfileRequest(DisplayName: name), cancellationToken).ConfigureAwait(false)).Profile!;
            return CliCommandExecutionResult.Ok($"Profile created: {profile.Name} ({profile.Id}).", ToData(profile, true));
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
        var lookup = await ResolveAsync(profileIdentifier, cancellationToken).ConfigureAwait(false);
        if (lookup.Error is not null)
        {
            return lookup.Error;
        }
        var profile = lookup.Profile!;

        try
        {
            var result = await _manageProfile.SwitchAsync(new ProfileRequest(Identifier: profile.Id), cancellationToken).ConfigureAwait(false);
            return CliCommandExecutionResult.Ok($"Switched to profile: {profile.Name} ({profile.Id}).", ToData(profile, string.Equals(result.ActiveProfileId, profile.Id, StringComparison.OrdinalIgnoreCase)));
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
        var lookup = await ResolveAsync(profileIdentifier, cancellationToken).ConfigureAwait(false);
        if (lookup.Error is not null)
        {
            return lookup.Error;
        }
        var profile = lookup.Profile!;

        try
        {
            var result = await _manageProfile.RenameAsync(new ProfileRequest(profile.Id, newName), cancellationToken).ConfigureAwait(false);
            var renamed = result.Profile!;
            return CliCommandExecutionResult.Ok($"Profile renamed: {renamed.Name} ({renamed.Id}).", ToData(renamed, string.Equals(result.ActiveProfileId, renamed.Id, StringComparison.OrdinalIgnoreCase)));
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

        var lookup = await ResolveAsync(profileIdentifier, cancellationToken).ConfigureAwait(false);
        if (lookup.Error is not null)
        {
            return lookup.Error;
        }
        var profile = lookup.Profile!;

        try
        {
            var result = await _manageProfile.DeleteAsync(new ProfileRequest(Identifier: profile.Id), cancellationToken).ConfigureAwait(false);
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

    private async Task<(ProfileInfo? Profile, CliCommandExecutionResult? Error)> ResolveAsync(
        string profileIdentifier,
        CancellationToken cancellationToken)
    {
        var result = await _manageProfile.ListAsync(cancellationToken).ConfigureAwait(false);
        var profile = result.Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, profileIdentifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Name, profileIdentifier, StringComparison.OrdinalIgnoreCase));

        if (profile is not null)
        {
            return (profile, null);
        }

        return (null, CliCommandExecutionResult.Fail(
            CliExitCode.InvalidArguments,
            "Profile not found.",
            [$"Unknown profile: {profileIdentifier}"]));
    }

    private static ProfileData ToData(ProfileInfo profile, bool isActive)
    {
        return new ProfileData(profile.Id, profile.Name, profile.CreatedAt, isActive);
    }
}
