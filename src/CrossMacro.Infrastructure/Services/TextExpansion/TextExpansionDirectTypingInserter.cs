using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal sealed class TextExpansionDirectTypingInserter
{
    private const int MaxBatchedInputEvents = 4096;
    private readonly IKeyboardLayoutService _layoutService;
    private readonly TextExpansionKeyDispatcher _keyDispatcher;

    public TextExpansionDirectTypingInserter(
        IKeyboardLayoutService layoutService,
        TextExpansionKeyDispatcher keyDispatcher)
    {
        ArgumentNullException.ThrowIfNull(layoutService);
        ArgumentNullException.ThrowIfNull(keyDispatcher);

        _layoutService = layoutService;
        _keyDispatcher = keyDispatcher;
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
            if (keyboardLayoutCharacter.HasValue &&
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
        DirectTypingMethod method = DirectTypingMethod.FastBatch)
    {
        ArgumentNullException.ThrowIfNull(inputSimulator);
        ArgumentNullException.ThrowIfNull(text);

        Log.Information(
            "Typing replacement directly (length={Length}, method={Method})",
            text.Length,
            method);
        if (method == DirectTypingMethod.FastBatch && TryInsertBatch(inputSimulator, text))
        {
            return;
        }

        var unicodeTextInput = inputSimulator as IUnicodeTextInputSimulator;
        var preferNativeUnicodeInjection = SupportsNativeUnicodeTextInput(unicodeTextInput);

        foreach (var element in TextExpansionTextElements.Enumerate(text))
        {
            if (element.IsNewLine)
            {
                await _keyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_ENTER);
                await Task.Delay(TextExpansionExecutionTimings.DirectTypingNewLineDelay);
                continue;
            }

            if (preferNativeUnicodeInjection)
            {
                await TypeUnicodeTextAsync(inputSimulator, unicodeTextInput, text, element);
            }
            else
            {
                var keyboardLayoutCharacter = element.KeyboardLayoutCharacter;
                var typedViaLayout = keyboardLayoutCharacter.HasValue &&
                    await TryTypeWithKeyboardLayoutAsync(inputSimulator, keyboardLayoutCharacter.Value);

                if (!typedViaLayout)
                {
                    await TypeUnicodeTextAsync(inputSimulator, unicodeTextInput, text, element);
                }
            }

            await Task.Delay(TextExpansionExecutionTimings.DirectTypingInterElementDelay);
        }
    }

    private bool TryInsertBatch(IInputSimulator inputSimulator, string text)
    {
        if (inputSimulator is not IBatchedInputSimulator { SupportsBatchedInput: true } batchedInputSimulator)
        {
            return false;
        }

        if (!TryBuildBatchSteps(text, out var steps))
        {
            return false;
        }

        if (steps.Count > MaxBatchedInputEvents)
        {
            Log.Debug(
                "Direct typing batch skipped because event count {EventCount} exceeds IPC batch limit {MaxEventCount}",
                steps.Count,
                MaxBatchedInputEvents);
            return false;
        }

        Log.Debug("Typing replacement using batched input (events={EventCount})", steps.Count);
        batchedInputSimulator.SimulateBatch(CollectionsMarshal.AsSpan(steps));
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
            if (!keyboardLayoutCharacter.HasValue || !TryResolveKeyboardLayoutInput(keyboardLayoutCharacter.Value, out var input))
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
        TimeSpan delayAfterKeyUp = default)
    {
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
        steps.Add(new InputSimulationStep(EV_SYN, SYN_REPORT, 0, ToDelayMilliseconds(delayAfter)));
    }

    private static int ToDelayMilliseconds(TimeSpan delay)
    {
        return delay <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(delay.TotalMilliseconds);
    }

    private async Task<bool> TryTypeWithKeyboardLayoutAsync(IInputSimulator inputSimulator, char character)
    {
        if (!TryResolveKeyboardLayoutInput(character, out var input))
        {
            return false;
        }

        await _keyDispatcher.SendKeyAsync(inputSimulator, input.KeyCode, input.Shift, input.AltGr);
        return true;
    }

    private static bool SupportsNativeUnicodeTextInput(IUnicodeTextInputSimulator? unicodeTextInput)
    {
        return unicodeTextInput?.SupportsUnicodeTextInput == true;
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
        TextExpansionTextElement element)
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
            await TypeLinuxUnicodeHexAsync(inputSimulator, element.CodePoint);
            return;
        }

        throw new PlatformNotSupportedException(
            $"Direct typing cannot inject character U+{element.CodePoint:X} on this platform without native Unicode text input support.");
    }

    [SupportedOSPlatform("linux")]
    private async Task TypeLinuxUnicodeHexAsync(IInputSimulator inputSimulator, int codePoint)
    {
        var composeSequence = ResolveLinuxUnicodeComposeSequence(codePoint);

        await _keyDispatcher.SendKeyAsync(
            inputSimulator,
            composeSequence.PrefixInput.KeyCode,
            shift: true,
            altGr: composeSequence.PrefixInput.AltGr,
            ctrl: true);

        await Task.Delay(TextExpansionExecutionTimings.LinuxUnicodeComposeActivationDelay);

        foreach (var hexInput in composeSequence.HexInputs)
        {
            await _keyDispatcher.SendKeyAsync(inputSimulator, hexInput.KeyCode, hexInput.Shift, hexInput.AltGr);
            await Task.Delay(TextExpansionExecutionTimings.LinuxUnicodeComposeInterKeyDelay);
        }

        await Task.Delay(TextExpansionExecutionTimings.LinuxUnicodeComposeCompletionDelay);
        await _keyDispatcher.SendKeyAsync(inputSimulator, InputEventCode.KEY_ENTER);
    }

    private LinuxUnicodeComposeSequence ResolveLinuxUnicodeComposeSequence(int codePoint)
    {
        var unicodePrefixInput = ResolveRequiredLinuxKeyboardLayoutInput(
            primary: 'u',
            alternate: 'U',
            failureMessage:
                "Current keyboard layout cannot start Linux unicode input because neither 'u' nor 'U' is available for the Ctrl+Shift+U sequence.");

        var hex = codePoint.ToString("x");
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
            failureMessage: alternateHexDigit.HasValue
                ? $"Current keyboard layout cannot type Linux unicode hex digit '{hexDigit}' or '{alternateHexDigit.Value}' required for code point U+{codePoint:X}."
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

        if (alternate.HasValue && TryResolveKeyboardLayoutInput(alternate.Value, out var alternateInput))
        {
            return alternateInput;
        }

        throw new InvalidOperationException(failureMessage);
    }

    private bool TryResolveKeyboardLayoutInput(char character, out KeyboardLayoutInput input)
    {
        var resolvedInput = _layoutService.GetInputForChar(character);
        if (resolvedInput.HasValue)
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
