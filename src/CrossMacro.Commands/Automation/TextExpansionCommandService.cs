
namespace CrossMacro.Commands.Automation;

public sealed class TextExpansionCommandService(IManageTextExpansion manageTextExpansion) : ITextExpansionCommandService
{
    private readonly IManageTextExpansion _manageTextExpansion = manageTextExpansion;

    public async Task<OperationResult> ListAsync(string? profileIdentifier, CancellationToken cancellationToken)
    {
        try
        {
            var expansions = await _manageTextExpansion.ListAsync(profileIdentifier, cancellationToken).ConfigureAwait(false);
            var data = new TextExpansionListData(expansions.Select(ToData).ToList(), profileIdentifier ?? string.Empty, expansions.Count);
            return OperationResult.Ok($"{expansions.Count} text expansion(s).", data);
        }
        catch (KeyNotFoundException)
        {
            return MissingProfileResult(profileIdentifier);
        }
    }

    public async Task<OperationResult> AddAsync(
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
            return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Text expansion trigger cannot be empty.");
        }

        try
        {
            var expansion = new TextExpansionEntry(trigger, replacement, isEnabled: true, method, insertionMode, directTypingMethod);
            _ = await _manageTextExpansion.AddAsync(expansion, profileIdentifier, cancellationToken).ConfigureAwait(false);
            return OperationResult.Ok($"Text expansion added: {trigger}.", ToData(expansion));
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

    public Task<OperationResult> RemoveAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) =>
        UpdateExistingAsync(
            trigger,
            profileIdentifier,
            token => _manageTextExpansion.RemoveAsync(trigger, profileIdentifier, token),
            $"Text expansion removed: {trigger}.",
            cancellationToken);

    public Task<OperationResult> EnableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken)
    {
        return SetEnabledAsync(trigger, isEnabled: true, profileIdentifier, cancellationToken);
    }

    public Task<OperationResult> DisableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken)
    {
        return SetEnabledAsync(trigger, isEnabled: false, profileIdentifier, cancellationToken);
    }

    public async Task<OperationResult> TestAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken)
    {
        try
        {
            var expansion = await _manageTextExpansion.FindAsync(trigger, profileIdentifier, cancellationToken).ConfigureAwait(false);
            if (expansion is null)
            {
                return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Text expansion not found.", [$"Unknown trigger: {trigger}"], data: new TextExpansionTestData(Found: false, Expansion: null));
            }
            return OperationResult.Ok(
                $"{expansion.Trigger} => {expansion.Replacement}",
                new TextExpansionTestData(Found: true, ToData(expansion)));
        }
        catch (KeyNotFoundException)
        {
            return MissingProfileResult(profileIdentifier);
        }
    }

    private Task<OperationResult> SetEnabledAsync(string trigger, bool isEnabled, string? profileIdentifier, CancellationToken cancellationToken) =>
        UpdateExistingAsync(
            trigger,
            profileIdentifier,
            token => _manageTextExpansion.SetEnabledAsync(trigger, isEnabled, profileIdentifier, token),
            $"Text expansion {(isEnabled ? "enabled" : "disabled")}: {trigger}.",
            cancellationToken);

    /// <summary>
    /// Looks the trigger up first so a missing trigger and a missing profile are told apart by what was found,
    /// not by the wording of the exception message.
    /// </summary>
    private async Task<OperationResult> UpdateExistingAsync(
        string trigger,
        string? profileIdentifier,
        Func<CancellationToken, Task<TextExpansionEntry>> apply,
        string successMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await _manageTextExpansion.FindAsync(trigger, profileIdentifier, cancellationToken).ConfigureAwait(false) is null)
            {
                return MissingTriggerResult(trigger);
            }
        }
        catch (KeyNotFoundException)
        {
            return MissingProfileResult(profileIdentifier);
        }

        try
        {
            var expansion = await apply(cancellationToken).ConfigureAwait(false);
            return OperationResult.Ok(successMessage, ToData(expansion));
        }
        catch (KeyNotFoundException)
        {
            // The trigger was removed between the lookup and the update.
            return MissingTriggerResult(trigger);
        }
    }

    private static OperationResult DuplicateTriggerResult(string trigger)
    {
        return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Duplicate text expansion trigger.", [$"Trigger already exists: {trigger}"]);
    }

    private static OperationResult MissingTriggerResult(string trigger)
    {
        return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Text expansion not found.", [$"Unknown trigger: {trigger}"]);
    }

    private static OperationResult MissingProfileResult(string? profileIdentifier)
    {
        return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Profile not found.", [$"Unknown profile: {profileIdentifier}"]);
    }

    private static TextExpansionData ToData(TextExpansionEntry expansion)
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
