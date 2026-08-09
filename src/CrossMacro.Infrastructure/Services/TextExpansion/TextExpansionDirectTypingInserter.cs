
namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal sealed class TextExpansionDirectTypingInserter
{
    private const int MaxBatchedInputEvents = 4096;
    private readonly IKeyboardLayoutService _layoutService;
    public TextExpansionDirectTypingInserter(
        IKeyboardLayoutService layoutService)
    {
        ArgumentNullException.ThrowIfNull(layoutService);

        _layoutService = layoutService;
    }

    public void ValidateSupport(IInputSimulator inputSimulator, string text)
    {
        ArgumentNullException.ThrowIfNull(inputSimulator);
        ArgumentNullException.ThrowIfNull(text);

        var unicodeTextInput = inputSimulator as IUnicodeTextInputSimulator;
        if (SupportsNativeUnicodeTextInput(unicodeTextInput))
        {
            return;
        }

        foreach (var element in TextExpansionTextElements.Enumerate(text))
        {
            if (element.IsNewLine)
            {
                continue;
            }

            var keyboardLayoutCharacter = element.KeyboardLayoutCharacter;
            if (keyboardLayoutCharacter is not null &&
                TryResolveKeyboardLayoutInput(keyboardLayoutCharacter.Value, out _))
            {
                continue;
            }

            ValidateUnicodeTextSupport(unicodeTextInput, element.CodePoint);
        }
    }

    public async Task InsertAsync(
        IInputSimulator inputSimulator,
        string text,
        DirectTypingMethod method = DirectTypingMethod.FastBatch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputSimulator);
        ArgumentNullException.ThrowIfNull(text);

        Log.Information(
            "Typing replacement directly (length={Length}, method={Method})",
            text.Length,
            method);
        cancellationToken.ThrowIfCancellationRequested();
        if (method is DirectTypingMethod.FastBatch && await TryInsertBatchAsync(inputSimulator, text, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (method is DirectTypingMethod.CompatibleKeyByKey && await TryInsertCompatibleBatchAsync(inputSimulator, text, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var unicodeTextInput = inputSimulator as IUnicodeTextInputSimulator;
        var preferNativeUnicodeInjection = SupportsNativeUnicodeTextInput(unicodeTextInput);

        foreach (var element in TextExpansionTextElements.Enumerate(text))
        {
            if (element.IsNewLine)
            {
                await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_ENTER, cancellationToken: cancellationToken).ConfigureAwait(false);
                await Task.Delay(TextExpansionExecutionTimings.DirectTypingNewLineDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (preferNativeUnicodeInjection)
            {
                await TypeUnicodeTextAsync(inputSimulator, unicodeTextInput, text, element, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var keyboardLayoutCharacter = element.KeyboardLayoutCharacter;
                var typedViaLayout = keyboardLayoutCharacter is not null &&
                    await TryTypeWithKeyboardLayoutAsync(inputSimulator, keyboardLayoutCharacter.Value, cancellationToken).ConfigureAwait(false);

                if (!typedViaLayout)
                {
                    await TypeUnicodeTextAsync(inputSimulator, unicodeTextInput, text, element, cancellationToken).ConfigureAwait(false);
                }
            }

            await Task.Delay(TextExpansionExecutionTimings.DirectTypingInterElementDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> TryInsertBatchAsync(IInputSimulator inputSimulator, string text, CancellationToken cancellationToken)
    {
        if (inputSimulator is not IBatchedInputSimulator { SupportsBatchedInput: true } batchedInputSimulator ||
            !TryBuildBatchSteps(text, out var steps) ||
            steps.Count > MaxBatchedInputEvents)
        {
            return false;
        }

        await SimulateBatchCoreAsync(batchedInputSimulator, steps, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> TryInsertCompatibleBatchAsync(IInputSimulator inputSimulator, string text, CancellationToken cancellationToken)
    {
        if (inputSimulator is not IBatchedInputSimulator { SupportsBatchedInput: true } batchedInputSimulator ||
            !TryBuildCompatibleBatchSteps(text, out var steps) ||
            steps.Count > MaxBatchedInputEvents)
        {
            return false;
        }

        await SimulateBatchCoreAsync(batchedInputSimulator, steps, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static Task SimulateBatchCoreAsync(
        IBatchedInputSimulator batchedInputSimulator,
        List<InputSimulationStep> steps,
        CancellationToken cancellationToken)
    {
        if (batchedInputSimulator is IAsyncBatchedInputSimulator asyncBatchedInputSimulator)
        {
            return asyncBatchedInputSimulator.SimulateBatchAsync(steps, cancellationToken);
        }

        // Fall back to sync batch for simulators without the async interface (e.g. legacy
        // uinput); otherwise batching silently degrades to key-by-key typing.
        cancellationToken.ThrowIfCancellationRequested();
        batchedInputSimulator.SimulateBatch(CollectionsMarshal.AsSpan(steps));
        return Task.CompletedTask;
    }

    private bool TryBuildCompatibleBatchSteps(string text, out List<InputSimulationStep> steps)
    {
        steps = [];
        foreach (var element in TextExpansionTextElements.Enumerate(text))
        {
            if (element.IsNewLine)
            {
                AddKeyPressSteps(
                    steps,
                    InputEventCode.KEY_ENTER,
                    keyPressReleaseDelay: TextExpansionExecutionTimings.CompatibleKeyPressReleaseDelay,
                    delayAfterKeyUp: TextExpansionExecutionTimings.CompatibleInterElementDelay);
                continue;
            }

            var keyboardLayoutCharacter = element.KeyboardLayoutCharacter;
            if (keyboardLayoutCharacter is null || !TryResolveKeyboardLayoutInput(keyboardLayoutCharacter.Value, out var input))
            {
                return false;
            }

            AddKeyPressSteps(
                steps,
                input.KeyCode,
                input.Shift,
                input.AltGr,
                keyPressReleaseDelay: TextExpansionExecutionTimings.CompatibleKeyPressReleaseDelay,
                delayAfterKeyUp: TextExpansionExecutionTimings.CompatibleInterElementDelay,
                modifierSettleDelay: TextExpansionExecutionTimings.CompatibleModifierSettleDelay);
        }

        return true;
    }

    private bool TryBuildBatchSteps(string text, out List<InputSimulationStep> steps)
    {
        steps = [];
        foreach (var element in TextExpansionTextElements.Enumerate(text))
        {
            if (element.IsNewLine)
            {
                AddKeyPressSteps(
                    steps,
                    InputEventCode.KEY_ENTER,
                    keyPressReleaseDelay: TextExpansionExecutionTimings.BatchedKeyPressReleaseDelay,
                    delayAfterKeyUp: TextExpansionExecutionTimings.BatchedDirectTypingInterElementDelay);
                continue;
            }

            var keyboardLayoutCharacter = element.KeyboardLayoutCharacter;
            if (keyboardLayoutCharacter is null || !TryResolveKeyboardLayoutInput(keyboardLayoutCharacter.Value, out var input))
            {
                return false;
            }

            AddKeyPressSteps(
                steps,
                input.KeyCode,
                input.Shift,
                input.AltGr,
                keyPressReleaseDelay: TextExpansionExecutionTimings.BatchedKeyPressReleaseDelay,
                delayAfterKeyUp: TextExpansionExecutionTimings.BatchedDirectTypingInterElementDelay);
        }

        return true;
    }

    private static void AddKeyPressSteps(
        List<InputSimulationStep> steps,
        int keyCode,
        bool shift = false,
        bool altGr = false,
        bool ctrl = false,
        TimeSpan keyPressReleaseDelay = default,
        TimeSpan delayAfterKeyUp = default,
        TimeSpan modifierSettleDelay = default)
    {
        bool hasModifier = ctrl || shift || altGr;

        if (ctrl)
        {
            AddKeyStateSteps(steps, InputEventCode.KEY_LEFTCTRL, pressed: true);
        }

        if (shift)
        {
            AddKeyStateSteps(steps, InputEventCode.KEY_LEFTSHIFT, pressed: true);
        }

        if (altGr)
        {
            AddKeyStateSteps(steps, InputEventCode.KEY_RIGHTALT, pressed: true);
        }

        if (hasModifier && modifierSettleDelay > TimeSpan.Zero)
        {
            AddDelayStep(steps, modifierSettleDelay);
        }

        AddKeyStateSteps(steps, keyCode, pressed: true, delayAfter: keyPressReleaseDelay);
        AddKeyStateSteps(steps, keyCode, pressed: false, delayAfter: delayAfterKeyUp);

        if (altGr)
        {
            AddKeyStateSteps(steps, InputEventCode.KEY_RIGHTALT, pressed: false);
        }

        if (shift)
        {
            AddKeyStateSteps(steps, InputEventCode.KEY_LEFTSHIFT, pressed: false);
        }

        if (ctrl)
        {
            AddKeyStateSteps(steps, InputEventCode.KEY_LEFTCTRL, pressed: false);
        }
    }

    private static void AddKeyStateSteps(
        List<InputSimulationStep> steps,
        int keyCode,
        bool pressed,
        TimeSpan delayAfter = default)
    {
        steps.Add(new InputSimulationStep(EV_KEY, (ushort)keyCode, pressed ? 1 : 0));
        steps.Add(new InputSimulationStep(EV_SYN, SYN_REPORT, 0, ToDelayMicroseconds(delayAfter)));
    }

    private static void AddDelayStep(List<InputSimulationStep> steps, TimeSpan delay)
    {
        steps.Add(new InputSimulationStep(EV_SYN, SYN_REPORT, 0, ToDelayMicroseconds(delay)));
    }

    private static long ToDelayMicroseconds(TimeSpan delay)
    {
        return delay <= TimeSpan.Zero ? 0 : (long)Math.Ceiling(delay.TotalMicroseconds);
    }

    private async Task<bool> TryTypeWithKeyboardLayoutAsync(IInputSimulator inputSimulator, char character, CancellationToken cancellationToken)
    {
        if (!TryResolveKeyboardLayoutInput(character, out var input))
        {
            return false;
        }

        await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, input.KeyCode, input.Shift, input.AltGr, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool SupportsNativeUnicodeTextInput(IUnicodeTextInputSimulator? unicodeTextInput)
    {
        return (unicodeTextInput?.SupportsUnicodeTextInput) is true;
    }

    private void ValidateUnicodeTextSupport(IUnicodeTextInputSimulator? unicodeTextInput, int codePoint)
    {
        if (SupportsNativeUnicodeTextInput(unicodeTextInput))
        {
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            _ = ResolveLinuxUnicodeComposeSequence(codePoint);
            return;
        }

        throw new PlatformNotSupportedException(
            $"Direct typing cannot inject character U+{codePoint:X} on this platform without native Unicode text input support.");
    }

    private async Task TypeUnicodeTextAsync(
        IInputSimulator inputSimulator,
        IUnicodeTextInputSimulator? unicodeTextInput,
        string sourceText,
        TextExpansionTextElement element,
        CancellationToken cancellationToken)
    {
        if (unicodeTextInput is { SupportsUnicodeTextInput: true } nativeUnicodeTextInput)
        {
            var unicodeText = element.GetText(sourceText);
            if (nativeUnicodeTextInput is ITaggedUnicodeTextInputSimulator taggedUnicodeTextInput)
            {
                taggedUnicodeTextInput.TypeTextTagged(unicodeText, InputEventMarkers.TextExpansionKeyboardEvent);
            }
            else
            {
                nativeUnicodeTextInput.TypeText(unicodeText);
            }

            return;
        }

        if (OperatingSystem.IsLinux())
        {
            await TypeLinuxUnicodeHexAsync(inputSimulator, element.CodePoint, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new PlatformNotSupportedException(
            $"Direct typing cannot inject character U+{element.CodePoint:X} on this platform without native Unicode text input support.");
    }

    [SupportedOSPlatform("linux")]
    private async Task TypeLinuxUnicodeHexAsync(IInputSimulator inputSimulator, int codePoint, CancellationToken cancellationToken)
    {
        var composeSequence = ResolveLinuxUnicodeComposeSequence(codePoint);

        await TextExpansionKeyDispatcher.SendKeyAsync(
            inputSimulator,
            composeSequence.PrefixInput.KeyCode,
            shift: true,
            altGr: composeSequence.PrefixInput.AltGr,
            ctrl: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await Task.Delay(TextExpansionExecutionTimings.LinuxUnicodeComposeActivationDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);

        foreach (var hexInput in composeSequence.HexInputs)
        {
            await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, hexInput.KeyCode, hexInput.Shift, hexInput.AltGr, cancellationToken: cancellationToken).ConfigureAwait(false);
            await Task.Delay(TextExpansionExecutionTimings.LinuxUnicodeComposeInterKeyDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
        }

        await Task.Delay(TextExpansionExecutionTimings.LinuxUnicodeComposeCompletionDelay, TimeProvider.System, cancellationToken).ConfigureAwait(false);
        await TextExpansionKeyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_ENTER, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private LinuxUnicodeComposeSequence ResolveLinuxUnicodeComposeSequence(int codePoint)
    {
        var unicodePrefixInput = ResolveRequiredLinuxKeyboardLayoutInput(
            primary: 'u',
            alternate: 'U',
            failureMessage:
                "Current keyboard layout cannot start Linux unicode input because neither 'u' nor 'U' is available for the Ctrl+Shift+U sequence.");

        var hex = codePoint.ToString("x", CultureInfo.InvariantCulture);
        var hexInputs = new KeyboardLayoutInput[hex.Length];

        for (int i = 0; i < hex.Length; i++)
        {
            hexInputs[i] = ResolveLinuxUnicodeHexDigitInput(hex[i], codePoint);
        }

        return new LinuxUnicodeComposeSequence(unicodePrefixInput, hexInputs);
    }

    private KeyboardLayoutInput ResolveLinuxUnicodeHexDigitInput(char hexDigit, int codePoint)
    {
        var alternateHexDigit = GetAlternateLinuxHexDigit(hexDigit);
        return ResolveRequiredLinuxKeyboardLayoutInput(
            primary: hexDigit,
            alternate: alternateHexDigit,
            failureMessage: alternateHexDigit is not null ? $"Current keyboard layout cannot type Linux unicode hex digit '{hexDigit}' or '{alternateHexDigit.Value}' required for code point U+{codePoint:X}."
                : $"Current keyboard layout cannot type Linux unicode hex digit '{hexDigit}' required for code point U+{codePoint:X}.");
    }

    private KeyboardLayoutInput ResolveRequiredLinuxKeyboardLayoutInput(
        char primary,
        char? alternate,
        string failureMessage)
    {
        if (TryResolveKeyboardLayoutInput(primary, out var primaryInput))
        {
            return primaryInput;
        }

        if (alternate is not null && TryResolveKeyboardLayoutInput(alternate.Value, out var alternateInput))
        {
            return alternateInput;
        }

        throw new InvalidOperationException(failureMessage);
    }

    private bool TryResolveKeyboardLayoutInput(char character, out KeyboardLayoutInput input)
    {
        var resolvedInput = _layoutService.GetInputForChar(character);
        if (resolvedInput is not null)
        {
            input = new KeyboardLayoutInput(
                resolvedInput.Value.KeyCode,
                resolvedInput.Value.Shift,
                resolvedInput.Value.AltGr);
            return true;
        }

        input = default;
        return false;
    }

    private static char? GetAlternateLinuxHexDigit(char hexDigit)
    {
        if (hexDigit is >= 'a' and <= 'f')
        {
            return char.ToUpperInvariant(hexDigit);
        }

        if (hexDigit is >= 'A' and <= 'F')
        {
            return char.ToLowerInvariant(hexDigit);
        }

        return null;
    }

    private readonly record struct KeyboardLayoutInput(int KeyCode, bool Shift, bool AltGr);

    private readonly record struct LinuxUnicodeComposeSequence(
        KeyboardLayoutInput PrefixInput,
        KeyboardLayoutInput[] HexInputs);

    private const ushort EV_KEY = 0x01;
    private const ushort EV_SYN = 0x00;
    private const ushort SYN_REPORT = 0x00;
}
