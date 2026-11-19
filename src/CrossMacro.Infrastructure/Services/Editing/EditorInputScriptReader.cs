using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Projects mouse, keyboard, delay, scroll and typing script steps into editable actions.</summary>
internal sealed class EditorInputScriptReader(IKeyCodeMapper keyCodeMapper, EditorEventProjection events)
{
    /// <summary>
    /// Mouse steps restore with the cursor position that the preceding <c>move</c> established, because
    /// a bare button step acts at that position.
    /// </summary>
    internal static bool TryReadMouseStep(string step, EditorMouseCursor cursor, [NotNullWhen(true)] out EditorAction? action)
    {
        if (TryParseMoveStep(step, out var moveMode, out var moveCoordinateSpace, out var moveX, out var moveY))
        {
            cursor.RecordMove(moveMode, moveCoordinateSpace, moveX, moveY);
            action = new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = moveMode is MouseCoordinateMode.Absolute,
                CoordinateSpace = moveCoordinateSpace,
                CoordinateXToken = moveX,
                CoordinateYToken = moveY,
            };
            return true;
        }

        if (TryParseButtonStep(step, out var keyword, out var button, out var isCurrentPositionExplicit))
        {
            action = cursor.TryGetPosition(isCurrentPositionExplicit, out var position)
                ? CreatePositionedButtonAction(keyword, button, position.IsAbsolute, position.CoordinateSpace, position.X, position.Y)
                : CreateCurrentPositionButtonAction(keyword, button);
            return true;
        }

        action = null;
        return false;
    }

    internal bool TryReadInputStep(string step, string stepForType, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (TryParseTapStep(step, out var tapKeyCode))
        {
            action = events.CreateKeyAction(EditorActionType.KeyPress, tapKeyCode);
        }
        else if (TryParseKeyStep(step, out var keyActionType, out var keyCode))
        {
            action = events.CreateKeyAction(keyActionType, keyCode);
        }
        else if (TryParseDelayStep(step, out var useRandomDelay, out var fixedDelay, out var randomMin, out var randomMax))
        {
            action = new EditorAction
            {
                Type = EditorActionType.Delay,
                UseRandomDelay = useRandomDelay,
                DelayMicroseconds = useRandomDelay ? 0 : fixedDelay,
                RandomDelayMinMs = useRandomDelay ? randomMin : 0,
                RandomDelayMaxMs = useRandomDelay ? randomMax : 0,
            };
        }
        else if (TryParseScrollStep(step, out var scrollActionType, out var scrollAmount))
        {
            action = new EditorAction { Type = scrollActionType, ScrollAmount = scrollAmount };
        }
        else if (RunScriptInputSyntax.TryParseType(stepForType, out var text))
        {
            action = new EditorAction { Type = EditorActionType.TextInput, Text = text };
        }

        return action is not null;
    }

    /// <summary>
    /// A <c>tap a+b</c> key combination is valid script that the structured key actions cannot express; it is
    /// kept as a raw step without the "unsupported" warning.
    /// </summary>
    internal bool IsKeyCombinationTapStep(string step)
    {
        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length is not 2 || !tokens[0].Equals("tap", StringComparison.OrdinalIgnoreCase) || !tokens[1].Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var keyToken in tokens[1].Split('+', StringSplitOptions.TrimEntries))
        {
            if (!TryResolveKeyCodeToken(keyToken, out _))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TryParseMoveStep(string step, out MouseCoordinateMode coordinateMode, out MouseCoordinateSpace coordinateSpace, out string x, out string y)
    {
        coordinateMode = MouseCoordinateMode.Relative;
        coordinateSpace = MouseCoordinateSpace.LogicalDesktop;
        x = string.Empty;
        y = string.Empty;
        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length is not 4 || !tokens[0].Equals("move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!RunScriptSyntax.TryParseMouseMoveMode(tokens[1], out coordinateMode, out coordinateSpace))
        {
            return false;
        }

        if (!EditorActionScriptTokens.TryParseNumericToken(tokens[2], out var xSourceType, out var xValue) || !EditorActionScriptTokens.TryParseNumericToken(tokens[3], out var ySourceType, out var yValue))
        {
            return false;
        }

        x = EditorActionScriptTokens.FormatNumericToken(xSourceType, xValue);
        y = EditorActionScriptTokens.FormatNumericToken(ySourceType, yValue);
        return true;
    }

    internal static bool TryParseButtonStep(string? rawStep, out string keyword, out MacroMouseButton button, out bool isCurrentPositionExplicit)
    {
        keyword = string.Empty;
        button = MacroMouseButton.Left;
        isCurrentPositionExplicit = false;
        if (string.IsNullOrWhiteSpace(rawStep))
        {
            return false;
        }

        var step = rawStep.Trim();
        keyword = step.Split(' ', 2)[0].ToUpperInvariant();
        return keyword is "CLICK" or "DOWN" or "UP" && RunScriptInputSyntax.TryParseButton(step, keyword, out button, out isCurrentPositionExplicit, out var error) && error is null;
    }

    private static EditorAction CreatePositionedButtonAction(string keyword, MacroMouseButton button, bool isAbsolute, MouseCoordinateSpace coordinateSpace, string x, string y)
    {
        return new EditorAction
        {
            Type = ToButtonActionType(keyword),
            Button = button,
            IsAbsolute = isAbsolute,
            CoordinateSpace = coordinateSpace,
            CoordinateXToken = x,
            CoordinateYToken = y,
            UseCurrentPosition = false,
        };
    }

    private static EditorAction CreateCurrentPositionButtonAction(string keyword, MacroMouseButton button)
    {
        return new EditorAction
        {
            Type = ToButtonActionType(keyword),
            Button = button,
            IsAbsolute = false,
            X = 0,
            Y = 0,
            UseCurrentPosition = true,
        };
    }

    private static EditorActionType ToButtonActionType(string keyword) => keyword switch
    {
        "CLICK" => EditorActionType.MouseClick,
        "DOWN" => EditorActionType.MouseDown,
        "UP" => EditorActionType.MouseUp,
        _ => EditorActionType.MouseClick,
    };

    private bool TryParseTapStep(string step, out int keyCode)
    {
        keyCode = 0;
        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length is not 2 || !tokens[0].Equals("tap", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keyToken = tokens[1];
        if (keyToken.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        return TryResolveKeyCodeToken(keyToken, out keyCode);
    }

    private bool TryParseKeyStep(string step, out EditorActionType actionType, out int keyCode)
    {
        actionType = EditorActionType.KeyDown;
        keyCode = 0;
        if (!RunScriptInputSyntax.TryParseKey(step, out var down, out var key, out var error) || error is not null)
        {
            return false;
        }

        actionType = down ? EditorActionType.KeyDown : EditorActionType.KeyUp;
        return TryResolveKeyCodeToken(key, out keyCode);
    }

    private bool TryResolveKeyCodeToken(string token, out int keyCode)
    {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out keyCode))
        {
            return keyCode > 0;
        }

        keyCode = keyCodeMapper.GetKeyCode(token);
        return keyCode > 0;
    }

    private static bool TryParseDelayStep(string step, out bool useRandomDelay, out long fixedDelayMicroseconds, out int randomMinDelayMs, out int randomMaxDelayMs) =>
        RunScriptInputSyntax.TryParseDelay(step, out useRandomDelay, out fixedDelayMicroseconds, out randomMinDelayMs, out randomMaxDelayMs, out var error) && error is null;

    private static bool TryParseScrollStep(string step, out EditorActionType actionType, out int amount)
    {
        actionType = EditorActionType.ScrollVertical;
        amount = 0;
        if (!RunScriptInputSyntax.TryParseScroll(step, out var button, out var count, out var error) || error is not null)
        {
            return false;
        }

        actionType = button is MacroMouseButton.ScrollLeft or MacroMouseButton.ScrollRight ? EditorActionType.ScrollHorizontal : EditorActionType.ScrollVertical;
        amount = button is MacroMouseButton.ScrollLeft or MacroMouseButton.ScrollDown ? -count : count;
        return true;
    }
}
