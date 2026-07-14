using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Automation;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class TextExpansionCliService : ITextExpansionCliService
{
    private readonly IManageTextExpansion _manageTextExpansion;

    public TextExpansionCliService(IManageTextExpansion manageTextExpansion)
    {
        _manageTextExpansion = manageTextExpansion;
    }

    internal TextExpansionCliService(ITextExpansionStore store, IProfileManager profileManager)
        : this(new ManageTextExpansion(store, profileManager))
    {
    }

    public async Task<CliCommandExecutionResult> ListAsync(string? profileIdentifier, CancellationToken cancellationToken)
    {
        try
        {
            var expansions = await _manageTextExpansion.ListAsync(profileIdentifier, cancellationToken).ConfigureAwait(false);
            var data = new TextExpansionListData(expansions.Select(ToData).ToList(), profileIdentifier ?? string.Empty, expansions.Count);
            return CliCommandExecutionResult.Ok($"{expansions.Count} text expansion(s).", data);
        }
        catch (KeyNotFoundException)
        {
            return MissingProfileResult(profileIdentifier);
        }
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

        try
        {
            var expansion = new TextExpansion(trigger, replacement, true, method, insertionMode, directTypingMethod);
            await _manageTextExpansion.AddAsync(expansion, profileIdentifier, cancellationToken).ConfigureAwait(false);
            return CliCommandExecutionResult.Ok($"Text expansion added: {trigger}.", ToData(expansion));
        }
        catch (InvalidOperationException)
        {
            return DuplicateTriggerResult(trigger);
        }
        catch (KeyNotFoundException)
        {
            return MissingProfileResult(profileIdentifier);
        }
    }

    public async Task<CliCommandExecutionResult> RemoveAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken)
    {
        try
        {
            var expansion = await _manageTextExpansion.RemoveAsync(trigger, profileIdentifier, cancellationToken).ConfigureAwait(false);
            return CliCommandExecutionResult.Ok($"Text expansion removed: {trigger}.", ToData(expansion));
        }
        catch (KeyNotFoundException ex) when (ex.Message.StartsWith("No text expansion", StringComparison.Ordinal))
        {
            return MissingTriggerResult(trigger);
        }
        catch (KeyNotFoundException)
        {
            return MissingProfileResult(profileIdentifier);
        }
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
        try
        {
            var expansion = await _manageTextExpansion.FindAsync(trigger, profileIdentifier, cancellationToken).ConfigureAwait(false);
            if (expansion is null)
            {
                return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Text expansion not found.", [$"Unknown trigger: {trigger}"], data: new TextExpansionTestData(false, null));
            }
            return CliCommandExecutionResult.Ok(
                $"{expansion.Trigger} => {expansion.Replacement}",
                new TextExpansionTestData(true, ToData(expansion)));
        }
        catch (KeyNotFoundException)
        {
            return MissingProfileResult(profileIdentifier);
        }
    }

    private async Task<CliCommandExecutionResult> SetEnabledAsync(string trigger, bool isEnabled, string? profileIdentifier, CancellationToken cancellationToken)
    {
        try
        {
            var expansion = await _manageTextExpansion.SetEnabledAsync(trigger, isEnabled, profileIdentifier, cancellationToken).ConfigureAwait(false);
            var verb = isEnabled ? "enabled" : "disabled";
            return CliCommandExecutionResult.Ok($"Text expansion {verb}: {trigger}.", ToData(expansion));
        }
        catch (KeyNotFoundException ex) when (ex.Message.StartsWith("No text expansion", StringComparison.Ordinal))
        {
            return MissingTriggerResult(trigger);
        }
        catch (KeyNotFoundException)
        {
            return MissingProfileResult(profileIdentifier);
        }
    }

    private static CliCommandExecutionResult DuplicateTriggerResult(string trigger)
    {
        return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Duplicate text expansion trigger.", [$"Trigger already exists: {trigger}"]);
    }

    private static CliCommandExecutionResult MissingTriggerResult(string trigger)
    {
        return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Text expansion not found.", [$"Unknown trigger: {trigger}"]);
    }

    private static CliCommandExecutionResult MissingProfileResult(string? profileIdentifier)
    {
        return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Profile not found.", [$"Unknown profile: {profileIdentifier}"]);
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
