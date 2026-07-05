using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;

namespace CrossMacro.Cli.Services;

public sealed class TextExpansionCliService : ITextExpansionCliService
{
    private readonly ITextExpansionStorageService _storageService;
    private readonly IProfileManager _profileManager;

    public TextExpansionCliService(ITextExpansionStorageService storageService, IProfileManager profileManager)
    {
        _storageService = storageService;
        _profileManager = profileManager;
    }

    public async Task<CliCommandExecutionResult> ListAsync(string? profileIdentifier, CancellationToken cancellationToken)
    {
        return await WithProfileAsync(profileIdentifier, async profileId =>
        {
            var expansions = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var data = new TextExpansionListData(expansions.Select(ToData).ToList(), profileId, expansions.Count);
            return CliCommandExecutionResult.Ok($"{expansions.Count} text expansion(s).", data);
        }).ConfigureAwait(false);
    }

    public async Task<CliCommandExecutionResult> AddAsync(
        string trigger,
        string replacement,
        PasteMethod method,
        TextInsertionMode insertionMode,
        DirectTypingMethod directTypingMethod,
        string? profileIdentifier,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Text expansion trigger cannot be empty.");
        }

        return await WithProfileAsync(profileIdentifier, async _ =>
        {
            var expansions = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (FindByTrigger(expansions, trigger) is not null)
            {
                return DuplicateTriggerResult(trigger);
            }

            var expansion = new TextExpansion(trigger, replacement, true, method, insertionMode, directTypingMethod);
            expansions.Add(expansion);
            await _storageService.SaveAsync(expansions).ConfigureAwait(false);
            return CliCommandExecutionResult.Ok($"Text expansion added: {trigger}.", ToData(expansion));
        }).ConfigureAwait(false);
    }

    public async Task<CliCommandExecutionResult> RemoveAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken)
    {
        return await WithProfileAsync(profileIdentifier, async _ =>
        {
            var expansions = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var expansion = FindByTrigger(expansions, trigger);
            if (expansion is null)
            {
                return MissingTriggerResult(trigger);
            }

            expansions.Remove(expansion);
            await _storageService.SaveAsync(expansions).ConfigureAwait(false);
            return CliCommandExecutionResult.Ok($"Text expansion removed: {trigger}.", ToData(expansion));
        }).ConfigureAwait(false);
    }

    public Task<CliCommandExecutionResult> EnableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken)
    {
        return SetEnabledAsync(trigger, true, profileIdentifier, cancellationToken);
    }

    public Task<CliCommandExecutionResult> DisableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken)
    {
        return SetEnabledAsync(trigger, false, profileIdentifier, cancellationToken);
    }

    public async Task<CliCommandExecutionResult> TestAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken)
    {
        return await WithProfileAsync(profileIdentifier, async _ =>
        {
            var expansions = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var expansion = FindByTrigger(expansions, trigger);
            if (expansion is null)
            {
                return CliCommandExecutionResult.Fail(
                    CliExitCode.InvalidArguments,
                    "Text expansion not found.",
                    [$"Unknown trigger: {trigger}"],
                    data: new TextExpansionTestData(false, null));
            }

            return CliCommandExecutionResult.Ok(
                $"{expansion.Trigger} => {expansion.Replacement}",
                new TextExpansionTestData(true, ToData(expansion)));
        }).ConfigureAwait(false);
    }

    private async Task<CliCommandExecutionResult> SetEnabledAsync(string trigger, bool isEnabled, string? profileIdentifier, CancellationToken cancellationToken)
    {
        return await WithProfileAsync(profileIdentifier, async _ =>
        {
            var expansions = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var expansion = FindByTrigger(expansions, trigger);
            if (expansion is null)
            {
                return MissingTriggerResult(trigger);
            }

            expansion.IsEnabled = isEnabled;
            await _storageService.SaveAsync(expansions).ConfigureAwait(false);
            var verb = isEnabled ? "enabled" : "disabled";
            return CliCommandExecutionResult.Ok($"Text expansion {verb}: {trigger}.", ToData(expansion));
        }).ConfigureAwait(false);
    }

    private async Task<CliCommandExecutionResult> WithProfileAsync(string? profileIdentifier, Func<string, Task<CliCommandExecutionResult>> operation)
    {
        var activeProfileId = _profileManager.ActiveProfile.Id;
        if (string.IsNullOrWhiteSpace(profileIdentifier))
        {
            return await operation(activeProfileId).ConfigureAwait(false);
        }

        var profile = _profileManager.Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, profileIdentifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Name, profileIdentifier, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Profile not found.",
                [$"Unknown profile: {profileIdentifier}"]);
        }

        await _storageService.ReloadAsync(_profileManager.GetProfileDirectory(profile.Id)).ConfigureAwait(false);
        try
        {
            return await operation(profile.Id).ConfigureAwait(false);
        }
        finally
        {
            await _storageService.ReloadAsync(_profileManager.GetProfileDirectory(activeProfileId)).ConfigureAwait(false);
        }
    }

    private async Task<List<TextExpansion>> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _storageService.LoadAsync().ConfigureAwait(false);
    }

    private static TextExpansion? FindByTrigger(IEnumerable<TextExpansion> expansions, string trigger)
    {
        return expansions.FirstOrDefault(expansion => string.Equals(expansion.Trigger, trigger, StringComparison.OrdinalIgnoreCase));
    }

    private static CliCommandExecutionResult DuplicateTriggerResult(string trigger)
    {
        return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Duplicate text expansion trigger.", [$"Trigger already exists: {trigger}"]);
    }

    private static CliCommandExecutionResult MissingTriggerResult(string trigger)
    {
        return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Text expansion not found.", [$"Unknown trigger: {trigger}"]);
    }

    private static TextExpansionData ToData(TextExpansion expansion)
    {
        return new TextExpansionData(
            expansion.Trigger,
            expansion.Replacement,
            expansion.IsEnabled,
            expansion.Method.ToString(),
            expansion.InsertionMode.ToString(),
            expansion.DirectTypingMethod.ToString());
    }
}
