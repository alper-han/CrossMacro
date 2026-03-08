using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

internal sealed class RunEventSequenceCompiler
{
    private readonly IKeyCodeMapper _keyCodeMapper;

    public RunEventSequenceCompiler(IKeyCodeMapper keyCodeMapper)
    {
        _keyCodeMapper = keyCodeMapper;
    }

    public RunStepCompileResult Compile(IReadOnlyList<RunStepEntry> expandedSteps)
    {
        var sequence = new MacroSequence
        {
            Name = "CLI Run Script",
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true
        };

        var timestampMs = 0L;
        var interEventDelayMs = 0;
        var initialDelayMs = 0;
        var hasEvents = false;
        bool? moveIsAbsolute = null;
        var hasAbsoluteCursorPosition = false;
        var absoluteCursorX = 0;
        var absoluteCursorY = 0;
        var hasUnpositionedMouseButtonSteps = false;

        for (var i = 0; i < expandedSteps.Count; i++)
        {
            var stepNumber = i + 1;
            var stepEntry = expandedSteps[i];
            var rawStep = stepEntry.Step;
            var fileLineNumber = stepEntry.FileLineNumber;
            var stepPrefix = fileLineNumber.HasValue
                ? $"Step {stepNumber} (line {fileLineNumber.Value})"
                : $"Step {stepNumber}";
            if (string.IsNullOrWhiteSpace(rawStep))
            {
                return RunStepCompileResult.Fail($"{stepPrefix}: step cannot be empty.");
            }

            var step = rawStep.Trim();
            try
            {
                if (TryParseDelay(step, out var delayMs))
                {
                    if (!hasEvents)
                    {
                        initialDelayMs += delayMs;
                    }
                    else
                    {
                        interEventDelayMs += delayMs;
                    }

                    continue;
                }

                if (TryParseMove(step, out var isAbsolute, out var x, out var y))
                {
                    if (moveIsAbsolute.HasValue && moveIsAbsolute.Value != isAbsolute)
                    {
                        return RunStepCompileResult.Fail($"{stepPrefix}: cannot mix absolute and relative move modes in a single run script.");
                    }

                    if (isAbsolute && hasUnpositionedMouseButtonSteps)
                    {
                        return RunStepCompileResult.Fail(
                            $"{stepPrefix}: absolute mode cannot be introduced after click/down/up steps. Place 'move abs <x> <y>' before mouse button steps.");
                    }

                    moveIsAbsolute ??= isAbsolute;
                    EmitEvent(new MacroEvent
                    {
                        Type = EventType.MouseMove,
                        X = x,
                        Y = y
                    });

                    if (isAbsolute)
                    {
                        hasAbsoluteCursorPosition = true;
                        absoluteCursorX = x;
                        absoluteCursorY = y;
                    }

                    continue;
                }

                if (TryParseButton(step, "down", out var downButton))
                {
                    var downEvent = new MacroEvent
                    {
                        Type = EventType.ButtonPress,
                        Button = downButton
                    };

                    if (moveIsAbsolute == true)
                    {
                        if (!hasAbsoluteCursorPosition)
                        {
                            return RunStepCompileResult.Fail(
                                $"{stepPrefix}: down <button> requires a prior 'move abs <x> <y>' step in absolute mode.");
                        }

                        downEvent.X = absoluteCursorX;
                        downEvent.Y = absoluteCursorY;
                    }
                    else if (moveIsAbsolute == null)
                    {
                        hasUnpositionedMouseButtonSteps = true;
                    }

                    EmitEvent(downEvent);

                    continue;
                }

                if (TryParseButton(step, "up", out var upButton))
                {
                    var upEvent = new MacroEvent
                    {
                        Type = EventType.ButtonRelease,
                        Button = upButton
                    };

                    if (moveIsAbsolute == true)
                    {
                        if (!hasAbsoluteCursorPosition)
                        {
                            return RunStepCompileResult.Fail(
                                $"{stepPrefix}: up <button> requires a prior 'move abs <x> <y>' step in absolute mode.");
                        }

                        upEvent.X = absoluteCursorX;
                        upEvent.Y = absoluteCursorY;
                    }
                    else if (moveIsAbsolute == null)
                    {
                        hasUnpositionedMouseButtonSteps = true;
                    }

                    EmitEvent(upEvent);

                    continue;
                }

                if (TryParseButton(step, "click", out var clickButton))
                {
                    var clickEvent = new MacroEvent
                    {
                        Type = EventType.Click,
                        Button = clickButton
                    };

                    if (moveIsAbsolute == true)
                    {
                        if (!hasAbsoluteCursorPosition)
                        {
                            return RunStepCompileResult.Fail(
                                $"{stepPrefix}: click <button> requires a prior 'move abs <x> <y>' step in absolute mode.");
                        }

                        clickEvent.X = absoluteCursorX;
                        clickEvent.Y = absoluteCursorY;
                    }
                    else if (moveIsAbsolute == null)
                    {
                        hasUnpositionedMouseButtonSteps = true;
                    }

                    EmitEvent(clickEvent);

                    continue;
                }

                if (TryParseScroll(step, out var scrollButton, out var scrollCount, out var scrollError))
                {
                    if (scrollError != null)
                    {
                        return RunStepCompileResult.Fail($"{stepPrefix}: {scrollError}");
                    }

                    for (var c = 0; c < scrollCount; c++)
                    {
                        EmitEvent(new MacroEvent
                        {
                            Type = EventType.Click,
                            Button = scrollButton
                        });
                    }

                    continue;
                }

                if (TryParseKey(step, out var isKeyDown, out var keyToken))
                {
                    var keyCode = ResolveKeyCode(keyToken);
                    if (keyCode < 0)
                    {
                        return RunStepCompileResult.Fail($"{stepPrefix}: unknown key '{keyToken}'.");
                    }

                    EmitEvent(new MacroEvent
                    {
                        Type = isKeyDown ? EventType.KeyPress : EventType.KeyRelease,
                        KeyCode = keyCode
                    });

                    continue;
                }

                if (TryParseTap(step, out var combo))
                {
                    var comboParts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (comboParts.Length == 0)
                    {
                        return RunStepCompileResult.Fail($"{stepPrefix}: tap combo cannot be empty.");
                    }

                    var modifiers = new List<int>();
                    var primaryKeys = new List<int>();
                    foreach (var part in comboParts)
                    {
                        var code = ResolveKeyCode(part);
                        if (code < 0)
                        {
                            return RunStepCompileResult.Fail($"{stepPrefix}: unknown key '{part}' in tap combo.");
                        }

                        if (_keyCodeMapper.IsModifierKeyCode(code))
                        {
                            modifiers.Add(code);
                        }
                        else
                        {
                            primaryKeys.Add(code);
                        }
                    }

                    if (primaryKeys.Count != 1)
                    {
                        return RunStepCompileResult.Fail($"{stepPrefix}: tap expects exactly one non-modifier key (example: ctrl+c).");
                    }

                    foreach (var modifier in modifiers.Distinct())
                    {
                        EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = modifier });
                    }

                    EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = primaryKeys[0] });
                    EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = primaryKeys[0] });

                    for (var m = modifiers.Count - 1; m >= 0; m--)
                    {
                        EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = modifiers[m] });
                    }

                    continue;
                }

                if (TryParseType(step, out var textToType))
                {
                    if (textToType.Length == 0)
                    {
                        return RunStepCompileResult.Fail($"{stepPrefix}: type text cannot be empty.");
                    }

                    foreach (var ch in textToType)
                    {
                        if (ch == '\r')
                        {
                            continue;
                        }

                        if (ch == '\n')
                        {
                            EmitTapKeyByName("Enter");
                            continue;
                        }

                        if (ch == '\t')
                        {
                            EmitTapKeyByName("Tab");
                            continue;
                        }

                        var keyCode = _keyCodeMapper.GetKeyCodeForCharacter(ch);
                        if (keyCode < 0)
                        {
                            return RunStepCompileResult.Fail($"{stepPrefix}: cannot map character '{ch}' for type command.");
                        }

                        var modifiers = new List<int>(2);
                        if (_keyCodeMapper.RequiresShift(ch))
                        {
                            modifiers.Add(ResolveKeyCode("Shift"));
                        }

                        if (_keyCodeMapper.RequiresAltGr(ch))
                        {
                            modifiers.Add(ResolveKeyCode("AltGr"));
                        }

                        foreach (var modifier in modifiers.Distinct())
                        {
                            if (modifier < 0)
                            {
                                return RunStepCompileResult.Fail($"{stepPrefix}: required modifier key is not available for type command.");
                            }
                            EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = modifier });
                        }

                        EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = keyCode });
                        EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = keyCode });

                        for (var m = modifiers.Count - 1; m >= 0; m--)
                        {
                            EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = modifiers[m] });
                        }
                    }

                    continue;
                }

                return RunStepCompileResult.Fail($"{stepPrefix}: unsupported step syntax '{rawStep}'.");
            }
            catch (ArgumentException ex)
            {
                return RunStepCompileResult.Fail($"{stepPrefix}: {ex.Message}");
            }
        }

        if (!hasEvents)
        {
            return RunStepCompileResult.Fail("Run script did not produce any executable events.");
        }

        sequence.IsAbsoluteCoordinates = moveIsAbsolute ?? false;
        sequence.TrailingDelayMs = interEventDelayMs;
        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type == EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e => e.Type == EventType.Click || e.Type == EventType.ButtonPress || e.Type == EventType.ButtonRelease);
        sequence.CalculateDuration();

        return RunStepCompileResult.Ok(sequence, initialDelayMs);

        void EmitEvent(MacroEvent ev)
        {
            ev.DelayMs = interEventDelayMs;
            timestampMs += interEventDelayMs;
            ev.Timestamp = timestampMs;
            interEventDelayMs = 0;
            sequence.Events.Add(ev);
            hasEvents = true;
        }

        void EmitTapKeyByName(string keyName)
        {
            var code = ResolveKeyCode(keyName);
            if (code < 0)
            {
                throw new ArgumentException($"Unknown key '{keyName}'.");
            }

            EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = code });
            EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = code });
        }
    }

    private int ResolveKeyCode(string keyToken)
    {
        if (int.TryParse(keyToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCode))
        {
            return parsedCode;
        }

        return _keyCodeMapper.GetKeyCode(keyToken);
    }

    private static bool TryParseDelay(string step, out int delayMs)
    {
        delayMs = 0;
        if (!step.StartsWith("delay ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[6..].Trim();
        if (payload.StartsWith("random ", StringComparison.OrdinalIgnoreCase))
        {
            var randomPayload = payload[7..].Trim();
            var randomParts = randomPayload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int minDelayMs;
            int maxDelayMs;

            if (randomParts.Length == 1 && randomParts[0].Contains("..", StringComparison.Ordinal))
            {
                var range = randomParts[0].Split("..", 2, StringSplitOptions.TrimEntries);
                if (range.Length != 2
                    || !int.TryParse(range[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minDelayMs)
                    || !int.TryParse(range[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxDelayMs))
                {
                    throw new ArgumentException("Invalid random delay range. Expected: delay random <min> <max> or delay random <min>..<max>.");
                }
            }
            else if (randomParts.Length == 2
                && int.TryParse(randomParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minDelayMs)
                && int.TryParse(randomParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxDelayMs))
            {
            }
            else
            {
                throw new ArgumentException("Invalid random delay syntax. Expected: delay random <min> <max> or delay random <min>..<max>.");
            }

            if (minDelayMs < 0 || maxDelayMs < 0 || minDelayMs > maxDelayMs)
            {
                throw new ArgumentException("Invalid random delay bounds. Expected 0 <= min <= max.");
            }

            delayMs = (int)Random.Shared.NextInt64(minDelayMs, (long)maxDelayMs + 1L);
            return true;
        }

        if (!int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out delayMs) || delayMs < 0)
        {
            throw new ArgumentException("Invalid delay value. Expected: delay <ms> with ms >= 0.");
        }

        return true;
    }

    private static bool TryParseMove(string step, out bool isAbsolute, out int x, out int y)
    {
        isAbsolute = false;
        x = 0;
        y = 0;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], "move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(parts[1], "abs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parts[1], "absolute", StringComparison.OrdinalIgnoreCase))
        {
            isAbsolute = true;
        }
        else if (string.Equals(parts[1], "rel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parts[1], "relative", StringComparison.OrdinalIgnoreCase))
        {
            isAbsolute = false;
        }
        else
        {
            throw new ArgumentException("Invalid move mode. Expected: abs|absolute|rel|relative.");
        }

        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
            || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
        {
            throw new ArgumentException("Invalid move coordinates. Expected integers.");
        }

        return true;
    }

    private static bool TryParseButton(string step, string command, out MouseButton button)
    {
        button = MouseButton.None;
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !string.Equals(parts[0], command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryResolveButton(parts[1], out button))
        {
            throw new ArgumentException($"Unknown mouse button '{parts[1]}'.");
        }

        return true;
    }

    private static bool TryParseKey(string step, out bool isKeyDown, out string keyToken)
    {
        isKeyDown = false;
        keyToken = string.Empty;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "key", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(parts[1], "down", StringComparison.OrdinalIgnoreCase))
        {
            isKeyDown = true;
        }
        else if (string.Equals(parts[1], "up", StringComparison.OrdinalIgnoreCase))
        {
            isKeyDown = false;
        }
        else
        {
            throw new ArgumentException("Invalid key action. Expected: key down <key> | key up <key>.");
        }

        keyToken = parts[2];
        return true;
    }

    private static bool TryParseTap(string step, out string combo)
    {
        combo = string.Empty;
        if (!step.StartsWith("tap ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        combo = step[4..].Trim();
        return true;
    }

    private static bool TryParseType(string step, out string text)
    {
        text = string.Empty;
        if (!step.StartsWith("type ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        text = step[5..];
        return true;
    }

    private static bool TryParseScroll(string step, out MouseButton button, out int count, out string? error)
    {
        button = MouseButton.None;
        count = 1;
        error = null;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts.Length > 3 || !string.Equals(parts[0], "scroll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        button = parts[1].ToLowerInvariant() switch
        {
            "up" => MouseButton.ScrollUp,
            "down" => MouseButton.ScrollDown,
            "left" => MouseButton.ScrollLeft,
            "right" => MouseButton.ScrollRight,
            _ => MouseButton.None
        };

        if (button == MouseButton.None)
        {
            error = "Unknown scroll direction. Expected: up|down|left|right.";
            return true;
        }

        if (parts.Length == 3
            && (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count <= 0))
        {
            error = "Invalid scroll count. Expected integer > 0.";
            return true;
        }

        return true;
    }

    private static bool TryResolveButton(string token, out MouseButton button)
    {
        button = token.ToLowerInvariant() switch
        {
            "left" or "l" => MouseButton.Left,
            "right" or "r" => MouseButton.Right,
            "middle" or "m" => MouseButton.Middle,
            "side1" or "side" or "back" => MouseButton.Side1,
            "side2" or "extra" or "forward" => MouseButton.Side2,
            _ => MouseButton.None
        };

        return button != MouseButton.None;
    }
}
