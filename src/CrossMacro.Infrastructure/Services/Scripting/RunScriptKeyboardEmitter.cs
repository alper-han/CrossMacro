namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>Compiles <c>key</c>, <c>tap</c> and <c>type</c> steps into key events.</summary>
internal sealed class RunScriptKeyboardEmitter(IKeyCodeMapper keyCodeMapper, RunScriptEventTimeline timeline)
{
    public bool TryEmitKey(string step, out string? error)
    {
        if (!RunScriptInputSyntax.TryParseKey(step, out var isKeyDown, out var keyToken, out error))
        {
            return false;
        }

        if (error is not null)
        {
            return true;
        }

        var keyCode = ResolveKeyCode(keyToken);
        if (keyCode < 0)
        {
            error = $"unknown key '{keyToken}'.";
            return true;
        }

        timeline.Emit(new MacroEvent
        {
            Type = isKeyDown ? EventType.KeyPress : EventType.KeyRelease,
            KeyCode = keyCode,
        });
        return true;
    }

    public bool TryEmitTap(string step, out string? error)
    {
        error = null;
        if (!RunScriptInputSyntax.TryParseTap(step, out var combo))
        {
            return false;
        }

        var parts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0)
        {
            error = "tap combo cannot be empty.";
            return true;
        }

        var modifiers = new List<int>();
        var primaryKeys = new List<int>();
        foreach (var part in parts)
        {
            var code = ResolveKeyCode(part);
            if (code < 0)
            {
                error = $"unknown key '{part}' in tap combo.";
                return true;
            }

            (keyCodeMapper.IsModifierKeyCode(code) ? modifiers : primaryKeys).Add(code);
        }

        var distinctModifiers = modifiers.Distinct().ToList();
        if (primaryKeys.Count is 0 && distinctModifiers.Count is 1)
        {
            EmitTap(distinctModifiers[0]);
            return true;
        }

        if (primaryKeys.Count is not 1)
        {
            error = "tap expects either exactly one non-modifier key (example: ctrl+c) or a single modifier key.";
            return true;
        }

        foreach (var modifier in distinctModifiers)
        {
            EmitKey(EventType.KeyPress, modifier);
        }

        EmitTap(primaryKeys[0]);
        for (var i = distinctModifiers.Count - 1; i >= 0; i--)
        {
            EmitKey(EventType.KeyRelease, distinctModifiers[i]);
        }

        return true;
    }

    public bool TryEmitType(string step, out string? error)
    {
        error = null;
        if (!RunScriptInputSyntax.TryParseType(step, out var text))
        {
            return false;
        }

        if (text.Length is 0)
        {
            error = "type text cannot be empty.";
            return true;
        }

        var isFirstCharacter = true;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            var controlKey = ControlKeyName(character);
            error = controlKey is not null
                ? TryEmitControlKey(controlKey, isFirstCharacter)
                : TryEmitCharacter(character, isFirstCharacter);
            if (error is not null)
            {
                return true;
            }

            isFirstCharacter = false;
        }

        return true;
    }

    private static string? ControlKeyName(char character) => character switch
    {
        '\r' or '\n' => "Enter",
        '\t' => "Tab",
        '\b' => "Backspace",
        _ => null,
    };

    private string? TryEmitControlKey(string keyName, bool isFirstCharacter)
    {
        var code = ResolveKeyCode(keyName);
        if (code < 0)
        {
            return $"Unknown key '{keyName}'.";
        }

        EmitTypedKey(code, isFirstCharacter);
        return null;
    }

    private string? TryEmitCharacter(char character, bool isFirstCharacter)
    {
        var keyCode = keyCodeMapper.GetKeyCodeForCharacter(character);
        if (keyCode < 0)
        {
            return $"cannot map character '{character}' for type command.";
        }

        var modifiers = new List<int>(2);
        if (keyCodeMapper.RequiresShift(character))
        {
            modifiers.Add(ResolveKeyCode("Shift"));
        }

        if (keyCodeMapper.RequiresAltGr(character))
        {
            modifiers.Add(ResolveKeyCode("AltGr"));
        }

        if (modifiers.Exists(static modifier => modifier < 0))
        {
            return "required modifier key is not available for type command.";
        }

        foreach (var modifier in modifiers.Distinct())
        {
            EmitKey(EventType.KeyPress, modifier);
        }

        EmitTypedKey(keyCode, isFirstCharacter);
        for (var i = modifiers.Count - 1; i >= 0; i--)
        {
            EmitKey(EventType.KeyRelease, modifiers[i]);
        }

        return null;
    }

    private int ResolveKeyCode(string keyToken) =>
        int.TryParse(keyToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCode)
            ? parsedCode
            : keyCodeMapper.GetKeyCode(keyToken);

    private void EmitKey(EventType type, int keyCode) => timeline.Emit(new MacroEvent { Type = type, KeyCode = keyCode });

    private void EmitTap(int keyCode)
    {
        EmitKey(EventType.KeyPress, keyCode);
        EmitKey(EventType.KeyRelease, keyCode);
    }

    // Typed characters are spaced by the editor's inter-key delay; the first one follows the previous step directly.
    private void EmitTypedKey(int keyCode, bool isFirstCharacter)
    {
        timeline.Emit(new MacroEvent
        {
            Type = EventType.KeyPress,
            KeyCode = keyCode,
            DelayMicroseconds = isFirstCharacter ? 0 : MacroTiming.DefaultKeyPressDelayMicroseconds,
        });
        EmitKey(EventType.KeyRelease, keyCode);
    }
}
