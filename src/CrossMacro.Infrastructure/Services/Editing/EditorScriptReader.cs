namespace CrossMacro.Infrastructure.Services.Editing;

internal sealed class EditorScriptReader
{
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly EditorEventProjection _events;
    internal EditorScriptReader(IKeyCodeMapper keyCodeMapper, EditorEventProjection events)
    {
        _keyCodeMapper = keyCodeMapper;
        _events = events;
    }

    internal bool TryRestoreActionsFromScriptSteps(IList<string>? scriptSteps, out List<EditorAction> actions, out List<EditorActionRestoreWarning> warnings)
    {
        actions = new List<EditorAction>();
        warnings = new List<EditorActionRestoreWarning>();
        if (scriptSteps is null || scriptSteps.Count is 0)
        {
            return false;
        }

        var hasAbsoluteCursorPosition = false;
        var absoluteCursorX = "0";
        var absoluteCursorY = "0";
        MouseCoordinateMode? currentMoveMode = null;
        MouseCoordinateSpace? currentMoveCoordinateSpace = null;
        for (var index = 0; index < scriptSteps.Count; index++)
        {
            var rawStep = scriptSteps[index];
            if (string.IsNullOrWhiteSpace(rawStep))
            {
                continue;
            }

            var step = rawStep.Trim();
            var stepForType = rawStep.TrimStart();
            if (TryParseMoveStep(step, out var moveMode, out var moveCoordinateSpace, out var moveX, out var moveY))
            {
                currentMoveMode = moveMode;
                currentMoveCoordinateSpace = moveCoordinateSpace;
                if (moveMode is MouseCoordinateMode.Absolute)
                {
                    hasAbsoluteCursorPosition = true;
                    absoluteCursorX = moveX;
                    absoluteCursorY = moveY;
                }
                else
                {
                    hasAbsoluteCursorPosition = false;
                }

                actions.Add(new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = moveMode is MouseCoordinateMode.Absolute, CoordinateSpace = moveCoordinateSpace, CoordinateXToken = moveX, CoordinateYToken = moveY, });
                continue;
            }

            if (TryParseButtonStep(step, out var currentButtonKeyword, out var currentButton, out var isCurrentPositionExplicit))
            {
                if (isCurrentPositionExplicit)
                {
                    actions.Add(CreateCurrentPositionButtonAction(currentButtonKeyword, currentButton));
                    continue;
                }

                if (currentMoveMode is MouseCoordinateMode.Absolute && hasAbsoluteCursorPosition)
                {
                    actions.Add(CreatePositionedButtonAction(currentButtonKeyword, currentButton, isAbsolute: true, MouseCoordinateSpace.LogicalDesktop, absoluteCursorX, absoluteCursorY));
                    continue;
                }

                if (currentMoveMode is MouseCoordinateMode.Relative)
                {
                    actions.Add(CreatePositionedButtonAction(currentButtonKeyword, currentButton, isAbsolute: false, currentMoveCoordinateSpace ?? MouseCoordinateSpace.LogicalDesktop, "0", "0"));
                    continue;
                }

                actions.Add(CreateCurrentPositionButtonAction(currentButtonKeyword, currentButton));
                continue;
            }

            if (TryParseTapStep(step, out var tapKeyCode))
            {
                actions.Add(_events.CreateKeyAction(EditorActionType.KeyPress, tapKeyCode));
                continue;
            }

            if (TryParseKeyStep(step, out var keyActionType, out var keyCode))
            {
                actions.Add(_events.CreateKeyAction(keyActionType, keyCode));
                continue;
            }

            if (TryParseDelayStep(step, out var useRandomDelay, out var fixedDelay, out var randomMin, out var randomMax))
            {
                actions.Add(new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = useRandomDelay, DelayMicroseconds = useRandomDelay ? 0 : fixedDelay, RandomDelayMinMs = useRandomDelay ? randomMin : 0, RandomDelayMaxMs = useRandomDelay ? randomMax : 0, });
                continue;
            }

            if (TryParseScrollStep(step, out var scrollActionType, out var scrollAmount))
            {
                actions.Add(new EditorAction { Type = scrollActionType, ScrollAmount = scrollAmount, });
                continue;
            }

            if (TryParseTypeStep(stepForType, out var text))
            {
                actions.Add(new EditorAction { Type = EditorActionType.TextInput, Text = text, });
                continue;
            }

            if (TryParseMousePositionStep(step, out var mousePositionAction))
            {
                actions.Add(mousePositionAction);
                continue;
            }

            if (TryParseSetStep(step, out var setAction))
            {
                actions.Add(setAction);
                continue;
            }

            if (TryParseScreenReadingStep(step, out var screenReadingAction))
            {
                actions.Add(screenReadingAction);
                continue;
            }

            if (TryParseClipboardStep(stepForType, out var clipboardAction))
            {
                actions.Add(clipboardAction);
                continue;
            }

            if (TryParseShellStep(stepForType, out var shellAction))
            {
                actions.Add(shellAction);
                continue;
            }

            if (RunScriptPlatformSyntax.IsScreenshotStep(stepForType))
            {
                if (TryParseScreenshotStep(stepForType, out var screenshotAction))
                {
                    actions.Add(screenshotAction);
                }
                else
                {
                    warnings.Add(new EditorActionRestoreWarning(index + 1, step, "Malformed screenshot step restored as raw script text."));
                    actions.Add(CreateRawScriptStepAction(step));
                }

                continue;
            }

            if (RunScriptSyntax.IsWindowStep(stepForType))
            {
                if (EditorWindowScriptReader.TryParseWindowStep(stepForType, out var windowAction))
                {
                    actions.Add(windowAction);
                }
                else
                {
                    warnings.Add(new EditorActionRestoreWarning(index + 1, step, "Malformed window step restored as raw script text."));
                    actions.Add(CreateRawScriptStepAction(step));
                }

                continue;
            }

            if (TryParseIncDecStep(step, "inc", EditorActionType.IncrementVariable, out var incrementAction))
            {
                actions.Add(incrementAction);
                continue;
            }

            if (TryParseIncDecStep(step, "dec", EditorActionType.DecrementVariable, out var decrementAction))
            {
                actions.Add(decrementAction);
                continue;
            }

            if (TryParseIncDecStep(step, "mul", EditorActionType.MultiplyVariable, out var multiplyAction))
            {
                actions.Add(multiplyAction);
                continue;
            }

            if (TryParseIncDecStep(step, "div", EditorActionType.DivideVariable, out var divideAction))
            {
                actions.Add(divideAction);
                continue;
            }

            if (TryParseRepeatStep(step, out var repeatAction))
            {
                actions.Add(repeatAction);
                continue;
            }

            if (TryParseConditionStep(step, "if", EditorActionType.IfBlockStart, out var ifAction))
            {
                actions.Add(ifAction);
                continue;
            }

            if (TryParseConditionStep(step, "while", EditorActionType.WhileBlockStart, out var whileAction))
            {
                actions.Add(whileAction);
                continue;
            }

            if (TryParseForStep(step, out var forAction))
            {
                actions.Add(forAction);
                continue;
            }

            if (RunScriptSyntax.IsElseHeader(step))
            {
                actions.Add(new EditorAction { Type = EditorActionType.ElseBlockStart });
                continue;
            }

            if (RunScriptSyntax.IsBreakCommand(step))
            {
                actions.Add(new EditorAction { Type = EditorActionType.Break });
                continue;
            }

            if (RunScriptSyntax.IsContinueCommand(step))
            {
                actions.Add(new EditorAction { Type = EditorActionType.Continue });
                continue;
            }

            if (RunScriptSyntax.IsBlockEndToken(step))
            {
                actions.Add(new EditorAction { Type = EditorActionType.BlockEnd });
                continue;
            }

            warnings.Add(new EditorActionRestoreWarning(index + 1, step, "Unsupported step restored as raw script text."));
            actions.Add(CreateRawScriptStepAction(step));
        }

        return actions.Count > 0;
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

    internal static bool TryParseButtonToken(string token, out MacroMouseButton button) => RunScriptInputSyntax.TryResolveButton(token, out button);
    internal static EditorAction CreatePositionedButtonAction(string keyword, MacroMouseButton button, bool isAbsolute, MouseCoordinateSpace coordinateSpace, string x, string y)
    {
        var actionType = keyword switch
        {
            "CLICK" => EditorActionType.MouseClick,
            "DOWN" => EditorActionType.MouseDown,
            "UP" => EditorActionType.MouseUp,
            _ => EditorActionType.MouseClick,
        };
        return new EditorAction
        {
            Type = actionType,
            Button = button,
            IsAbsolute = isAbsolute,
            CoordinateSpace = coordinateSpace,
            CoordinateXToken = x,
            CoordinateYToken = y,
            UseCurrentPosition = false,
        };
    }

    internal static EditorAction CreateCurrentPositionButtonAction(string keyword, MacroMouseButton button)
    {
        var actionType = keyword switch
        {
            "CLICK" => EditorActionType.MouseClick,
            "DOWN" => EditorActionType.MouseDown,
            "UP" => EditorActionType.MouseUp,
            _ => EditorActionType.MouseClick,
        };
        return new EditorAction
        {
            Type = actionType,
            Button = button,
            IsAbsolute = false,
            X = 0,
            Y = 0,
            UseCurrentPosition = true,
        };
    }

    internal static EditorAction CreateRawScriptStepAction(string step)
    {
        return new EditorAction
        {
            Type = EditorActionType.RawScriptStep,
            Text = step,
        };
    }

    internal bool TryParseTapStep(string step, out int keyCode)
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

    internal bool TryParseKeyStep(string step, out EditorActionType actionType, out int keyCode)
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

    internal bool TryResolveKeyCodeToken(string token, out int keyCode)
    {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out keyCode))
        {
            return keyCode > 0;
        }

        keyCode = _keyCodeMapper.GetKeyCode(token);
        return keyCode > 0;
    }

    internal static bool TryParseDelayStep(string step, out bool useRandomDelay, out long fixedDelayMicroseconds, out int randomMinDelayMs, out int randomMaxDelayMs) => RunScriptInputSyntax.TryParseDelay(step, out useRandomDelay, out fixedDelayMicroseconds, out randomMinDelayMs, out randomMaxDelayMs, out var error) && error is null;
    internal static bool TryParseScrollStep(string step, out EditorActionType actionType, out int amount)
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

    internal static bool TryParseTypeStep(string step, out string text) => RunScriptInputSyntax.TryParseType(step, out text);
    internal static bool TryParseMousePositionStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptSyntax.IsMousePositionStep(step))
        {
            return false;
        }

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 4 || !parts[1].Equals(RunScriptSyntax.MousePositionCommand, StringComparison.OrdinalIgnoreCase) || !TryNormalizeVariableName(parts[2], out var xVariable) || !TryNormalizeVariableName(parts[3], out var yVariable) || string.Equals(xVariable, yVariable, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        action = new EditorAction
        {
            Type = EditorActionType.MousePosition,
            MousePositionXVariableName = xVariable,
            MousePositionYVariableName = yVariable,
        };
        return true;
    }

    internal static bool TryParseClipboardStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptSyntax.StartsWithCommandToken(step.TrimStart(), RunScriptSyntax.ClipboardCommand))
        {
            return false;
        }

        var trimmed = step.Trim();
        var parts = trimmed.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 3 || !parts[0].Equals(RunScriptSyntax.ClipboardCommand, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts[1].Equals("get", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryNormalizeVariableName(parts[2], out var variableName))
            {
                return false;
            }

            action = new EditorAction
            {
                Type = EditorActionType.ClipboardGet,
                ScriptVariableName = variableName,
            };
            return true;
        }

        if (parts[1].Equals("set", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(parts[2]))
        {
            action = new EditorAction
            {
                Type = EditorActionType.ClipboardSet,
                Text = parts[2],
            };
            return true;
        }

        if (parts[1].Equals("capture", StringComparison.OrdinalIgnoreCase))
        {
            var captureParts = parts[2].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (captureParts.Length is not 2 || !ClipboardCopyShortcutSyntax.TryParse(captureParts[0], out var shortcut) || !TryNormalizeVariableName(captureParts[1], out var variableName))
            {
                return false;
            }

            action = new EditorAction
            {
                Type = EditorActionType.CopySelectionToVariable,
                ClipboardCopyShortcut = shortcut,
                ScriptVariableName = variableName,
            };
            return true;
        }

        return false;
    }

    internal static bool TryParseShellStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptShellSyntax.TryParse(step, out var parsed, out _) || parsed is null)
        {
            return false;
        }

        var payload = step.Trim()[RunScriptSyntax.ShellCommand.Length..].TrimStart();
        // Keep unquoted legacy command text opaque so editing does not rewrite its quoting.
        if (parsed.CaptureTargets is null && parsed.StandardInput is null && payload[0] is not ('"' or '\''))
        {
            return false;
        }

        var mode = (parsed.CaptureTargets is not null, parsed.StandardInput is not null) switch
        {
            (true, true) => ShellCommandMode.ShellCaptureInput,
            (true, false) => ShellCommandMode.ShellCapture,
            (false, true) => ShellCommandMode.ShellInput,
            _ => ShellCommandMode.Shell,
        };
        action = CreateShellAction(mode, parsed.Command, parsed.StandardInput ?? string.Empty, parsed.CaptureTargets?.ExitCodeVariable, parsed.CaptureTargets?.StandardOutputVariable, parsed.CaptureTargets?.StandardErrorVariable, parsed.Retries, parsed.BackoffMs, parsed.TimeoutMs);
        return true;
    }

    internal static bool TryParseScreenshotStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptPlatformSyntax.IsScreenshotStep(step))
        {
            return false;
        }

        if (!RunScriptPlatformSyntax.TryParseScreenshotStep(step, out var parsed, out _))
        {
            return false;
        }

        action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotOutputPath = parsed.OutputPath ?? string.Empty,
            ScreenshotCopyToClipboard = parsed.CopyToClipboard,
            ScreenshotUseRegion = parsed.UseRegion,
            ScreenshotRegionX = parsed.UseRegion ? parsed.RegionX : "0",
            ScreenshotRegionY = parsed.UseRegion ? parsed.RegionY : "0",
            ScreenshotRegionWidth = parsed.UseRegion ? parsed.RegionWidth : "100",
            ScreenshotRegionHeight = parsed.UseRegion ? parsed.RegionHeight : "100",
        };
        return true;
    }

    internal static EditorAction CreateShellAction(ShellCommandMode mode, string command, string standardInput, string? exitVariable, string? stdoutVariable, string? stderrVariable, int retries, int backoffMs, int timeoutMs)
    {
        return new EditorAction
        {
            Type = EditorActionType.ShellCommand,
            ShellCommandMode = mode,
            ShellCommand = command,
            ShellStandardInput = standardInput,
            ShellExitCodeVariableName = exitVariable ?? "exit_code",
            ShellStandardOutputVariableName = stdoutVariable ?? "stdout",
            ShellStandardErrorVariableName = stderrVariable ?? "stderr",
            ShellRetries = retries,
            ShellBackoffMs = backoffMs,
            ShellTimeoutMs = timeoutMs,
        };
    }

    internal static bool TryParseSetStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[4..].Trim();
        if (payload.Length is 0)
        {
            return false;
        }

        if (TryParseStructuredSetPayload(payload, out var variableName, out var valueType, out var value))
        {
            action = new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = variableName,
                ScriptValueType = valueType,
                ScriptValue = value,
            };
            return true;
        }

        action = new EditorAction
        {
            Type = EditorActionType.SetVariable,
            Text = payload,
        };
        return true;
    }

    internal static bool TryParseStructuredSetPayload(string payload, out string variableName, out ScriptValueType valueType, out string value)
    {
        variableName = string.Empty;
        valueType = ScriptValueType.Text;
        value = string.Empty;
        var equalIndex = payload.IndexOf('=', StringComparison.Ordinal);
        string rawName;
        string rawValue;
        if (equalIndex > 0)
        {
            rawName = payload[..equalIndex].Trim();
            rawValue = payload[(equalIndex + 1)..].Trim();
        }
        else
        {
            var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            rawName = parts[0];
            rawValue = parts[1].Trim();
        }

        if (!TryNormalizeVariableName(rawName, out variableName) || string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (!TryInferSetValue(rawValue, out valueType, out value))
        {
            return false;
        }

        return true;
    }

    internal static bool TryParseScreenReadingStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptScreenReadingStepParser.TryParseStep(step, out var parsed, out var error) || error is not null)
        {
            return false;
        }
        return parsed switch
        {
            ParsedScreenReadStep.PixelColor pixel => ProjectPixelColor(pixel, out action),
            ParsedScreenReadStep.WaitColor wait => ProjectWaitColor(wait, out action),
            ParsedScreenReadStep.PixelSearch search => ProjectPixelSearch(search, out action),
            ParsedScreenReadStep.Image image => ProjectImage(image, out action),
            _ => false,
        };
    }

    private static bool ProjectPixelColor(ParsedScreenReadStep.PixelColor step, out EditorAction action)
    {
        action = new EditorAction();
        action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForPixelColor(!step.Relative, step.X, step.Y,
            EditorActionScriptTokens.NormalizeVariableToken(step.ResultVariable ?? EditorActionScreenReadingPayload.DefaultColorVariableName)));
        return true;
    }

    private static bool ProjectWaitColor(ParsedScreenReadStep.WaitColor step, out EditorAction action)
    {
        action = new EditorAction();
        if (!TryParseTargetColorToken(step.ColorToken, out var source, out var color, out var variable))
        {
            return false;
        }
        action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForWaitColor(step.X, step.Y, color,
            step.TimeoutMs ?? EditorActionScreenReadingPayload.DefaultTimeoutMs,
            EditorActionScriptTokens.NormalizeVariableToken(step.ResultVariable ?? EditorActionScreenReadingPayload.DefaultColorVariableName)));
        action.ScreenTargetColorSource = source;
        action.ScreenTargetColorVariableName = variable;
        return true;
    }

    private static bool ProjectPixelSearch(ParsedScreenReadStep.PixelSearch step, out EditorAction action)
    {
        action = new EditorAction();
        if (!TryParseTargetColorToken(step.ColorToken, out var source, out var color, out var variable)
            || !TryGetPositiveRegionSize(step.X1, step.Y1, step.X2, step.Y2, out var width, out var height)) { return false; }
        action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForPixelSearch(step.X1, step.Y1, width, height, color,
            EditorActionScriptTokens.NormalizeVariableToken(step.Variables.FoundVariableName ?? EditorActionScreenReadingPayload.DefaultFoundVariableName),
            EditorActionScriptTokens.NormalizeVariableToken(step.Variables.XVariableName ?? EditorActionScreenReadingPayload.DefaultFoundXVariableName),
            EditorActionScriptTokens.NormalizeVariableToken(step.Variables.YVariableName ?? EditorActionScreenReadingPayload.DefaultFoundYVariableName), step.Tolerance));
        action.ScreenTimeoutMs = step.TimeoutMs ?? EditorActionScreenReadingPayload.DefaultTimeoutMs;
        action.ScreenTargetColorSource = source;
        action.ScreenTargetColorVariableName = variable;
        return true;
    }

    private static bool ProjectImage(ParsedScreenReadStep.Image step, out EditorAction action)
    {
        var legacy = step.Region as ParsedScreenReadStep.LegacyRegion;
        action = new EditorAction
        {
            Type = step.Command switch
            {
                RunScriptScreenReadingCommand.ImageClick => EditorActionType.ImageClick,
                RunScriptScreenReadingCommand.WaitImage => EditorActionType.WaitImage,
                RunScriptScreenReadingCommand.ImageSearch => EditorActionType.ImageSearch,
                RunScriptScreenReadingCommand.PixelColor or RunScriptScreenReadingCommand.WaitColor or RunScriptScreenReadingCommand.PixelSearch
                    => throw new ArgumentOutOfRangeException(nameof(step), step.Command, "Expected an image command."),
                _ => throw new ArgumentOutOfRangeException(nameof(step), step.Command, "Screen command is invalid."),
            },
            ScreenLeft = legacy?.Left ?? 0,
            ScreenTop = legacy?.Top ?? 0,
            ScreenWidth = legacy is null ? EditorActionScreenReadingPayload.DefaultSearchScreenWidth : legacy.Right - legacy.Left,
            ScreenHeight = legacy is null ? EditorActionScreenReadingPayload.DefaultSearchScreenHeight : legacy.Bottom - legacy.Top,
            ImageAssetName = step.ImageName,
            ScreenFoundVariableName = EditorActionScriptTokens.NormalizeVariableToken(step.Variables.FoundVariableName ?? EditorActionScreenReadingPayload.DefaultFoundVariableName),
            ScreenFoundXVariableName = EditorActionScriptTokens.NormalizeVariableToken(step.Variables.XVariableName ?? EditorActionScreenReadingPayload.DefaultFoundXVariableName),
            ScreenFoundYVariableName = EditorActionScriptTokens.NormalizeVariableToken(step.Variables.YVariableName ?? EditorActionScreenReadingPayload.DefaultFoundYVariableName),
            ScreenTimeoutMs = step.TimeoutMs ?? EditorActionScreenReadingPayload.DefaultTimeoutMs,
            ImageSearchSimilarity = step.Similarity,
            ImageSearchMatchMode = step.MatchMode,
            ImageSearchMatchModeWasExplicit = step.MatchModeExplicit,
            Button = step.Button,
        };
        if (step.Region is ParsedScreenReadStep.ExplicitRegion region)
        {
            action.ImageSearchRegionLeftToken = region.Left;
            action.ImageSearchRegionTopToken = region.Top;
            action.ImageSearchRegionWidthToken = region.Width;
            action.ImageSearchRegionHeightToken = region.Height;
        }
        return true;
    }

    internal static bool TryGetPositiveRegionSize(int left, int top, int right, int bottom, out int width, out int height)
    {
        var widthValue = (long)right - left;
        var heightValue = (long)bottom - top;
        if (widthValue <= 0 || heightValue <= 0 || widthValue > int.MaxValue || heightValue > int.MaxValue)
        {
            width = 0;
            height = 0;
            return false;
        }
        width = (int)widthValue;
        height = (int)heightValue;
        return true;
    }

    internal static bool TryParseTargetColorToken(string token, out EditorActionScreenTargetColorSource colorSource, out string colorHex, out string variableName)
    {
        colorSource = EditorActionScreenTargetColorSource.ManualHex;
        colorHex = EditorActionScreenReadingPayload.DefaultColorHex;
        variableName = EditorActionScreenReadingPayload.DefaultTargetColorVariableName;
        if (ScreenPixelColor.TryParse(token, out var color))
        {
            colorHex = color.ToString();
            return true;
        }

        if (!token.StartsWith('$') || !TryNormalizeVariableName(token, out variableName))
        {
            return false;
        }

        colorSource = EditorActionScreenTargetColorSource.Variable;
        return true;
    }

    internal static bool TryParseInteger(string value, out int result)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    internal static bool TryParseIncDecStep(string step, string keyword, EditorActionType actionType, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith($"{keyword} ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[(keyword.Length + 1)..].Trim();
        if (payload.Length is 0)
        {
            return false;
        }

        var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0)
        {
            return false;
        }

        if (!TryNormalizeVariableName(parts[0], out var variableName))
        {
            action = new EditorAction
            {
                Type = actionType,
                Text = payload,
            };
            return true;
        }

        var amountToken = parts.Length > 1 ? parts[1] : "1";
        if (!TryParseNumericToken(amountToken, out var sourceType, out var tokenValue))
        {
            action = new EditorAction
            {
                Type = actionType,
                Text = payload,
            };
            return true;
        }

        action = new EditorAction
        {
            Type = actionType,
            ScriptVariableName = variableName,
            ScriptNumericSourceType = sourceType,
            ScriptNumericValue = tokenValue,
        };
        return true;
    }

    internal static bool TryParseRepeatStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase) || !step.EndsWith('{'))
        {
            return false;
        }

        var token = step[7..^1].Trim();
        if (token.Length is 0)
        {
            return false;
        }

        // Binary count: store the canonical Format() string; source type mirrors the left operand.
        if (ScriptNumericExpression.TryParse(token, out var expression) && expression is { Op: not null })
        {
            action = new EditorAction
            {
                Type = EditorActionType.RepeatBlockStart,
                ScriptNumericSourceType = expression.LeftSource,
                ScriptNumericValue = ScriptNumericExpression.Format(expression),
            };
            return true;
        }

        if (TryParseNumericToken(token, out var sourceType, out var tokenValue))
        {
            action = new EditorAction
            {
                Type = EditorActionType.RepeatBlockStart,
                ScriptNumericSourceType = sourceType,
                ScriptNumericValue = tokenValue,
            };
            return true;
        }

        action = new EditorAction
        {
            Type = EditorActionType.RepeatBlockStart,
            Text = token,
        };
        return true;
    }

    internal static bool TryParseConditionStep(string step, string keyword, EditorActionType actionType, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptHeaderParser.TryReadConditionHeader(step, keyword, out var condition))
        {
            return false;
        }

        if (condition.Length is 0)
        {
            return false;
        }

        if (RunScriptConditionParser.TryParse(condition, out var parsedCondition, out _) && parsedCondition != null && TryMapConditionOperatorToken(parsedCondition.OperatorToken, out var conditionOperator))
        {
            var preferColor = conditionOperator is ScriptConditionOperator.Equals or ScriptConditionOperator.NotEquals;
            var allowArithmetic = conditionOperator is ScriptConditionOperator.GreaterThan or ScriptConditionOperator.GreaterThanOrEqual or ScriptConditionOperator.LessThan or ScriptConditionOperator.LessThanOrEqual;
            if (!TryParseConditionOperandToken(parsedCondition.LeftToken, allowArithmetic, preferColor, out var leftType, out var leftValue) || !TryParseConditionOperandToken(parsedCondition.RightToken, allowArithmetic, preferColor, out var rightType, out var rightValue))
            {
                return false;
            }

            action = new EditorAction
            {
                Type = actionType,
                ScriptLeftOperandType = leftType,
                ScriptLeftOperand = leftValue,
                ScriptConditionOperator = conditionOperator,
                ScriptRightOperandType = rightType,
                ScriptRightOperand = rightValue,
            };
            return true;
        }

        action = new EditorAction
        {
            Type = actionType,
            Text = condition,
        };
        return true;
    }

    internal static bool TryMapConditionOperatorToken(string operatorToken, out ScriptConditionOperator conditionOperator) =>
        ScriptConditionOperatorSyntax.TryParse(operatorToken, out conditionOperator);

    internal static bool TryParseForStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith("for ", StringComparison.OrdinalIgnoreCase) || !step.EndsWith('{'))
        {
            return false;
        }

        var body = step[4..^1].Trim();
        if (body.Length is 0)
        {
            return false;
        }

        action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            Text = body,
        };
        if (!RunScriptHeaderParser.TryParseForHeader(step, out var header, out var error) || error is not null || header is null)
        {
            return true; // Preserve unsupported legacy source for an explicit editor repair.
        }

        var start = header.StartToken.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var end = header.EndToken.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var stepTokens = (header.StepToken ?? "1").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!TryParseNumericSegment(start, 0, start.Length, out var startType, out var startValue) || !TryParseNumericSegment(end, 0, end.Length, out var endType, out var endValue) || !TryParseNumericSegment(stepTokens, 0, stepTokens.Length, out var stepType, out var stepValue))
        {
            return true;
        }

        action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            ForVariableName = header.VariableName,
            ForStartType = startType,
            ForStartValue = startValue,
            ForEndType = endType,
            ForEndValue = endValue,
            ForHasStep = header.HasExplicitStep,
            ForStepType = stepType,
            ForStepValue = stepValue,
        };
        return true;
    }

    internal static bool TryParseNumericSegment(string[] tokens, int startIndex, int endIndex, out ScriptNumericSourceType sourceType, out string value)
    {
        sourceType = ScriptNumericSourceType.Number;
        value = string.Empty;
        var count = endIndex - startIndex;
        if (count is 1)
        {
            return TryParseNumericToken(tokens[startIndex], out sourceType, out value);
        }

        // Segments are 1 or 3 tokens (longer falls back to raw text); expressions store canonical Format(), type mirrors the left operand.
        if (count is 3)
        {
            var raw = string.Join(' ', tokens, startIndex, count);
            if (ScriptNumericExpression.TryParse(raw, out var expression) && expression is { Op: not null })
            {
                sourceType = expression.LeftSource;
                value = ScriptNumericExpression.Format(expression);
                return true;
            }
        }

        return false;
    }

    internal static bool TryParseNumericToken(string rawToken, out ScriptNumericSourceType sourceType, out string tokenValue)
    {
        return EditorActionScriptTokens.TryParseNumericToken(rawToken, out sourceType, out tokenValue);
    }

    internal static bool TryParseOperandToken(string rawToken, out ScriptOperandType operandType, out string tokenValue, bool preferColor = false)
    {
        operandType = ScriptOperandType.Text;
        tokenValue = string.Empty;
        var token = rawToken.Trim();
        if (token.Length is 0)
        {
            return false;
        }

        if (token.StartsWith("$$", StringComparison.Ordinal))
        {
            operandType = ScriptOperandType.Text;
            tokenValue = EditorActionScriptTokens.UnescapeLiteralDollar(token);
            return true;
        }

        if (token.StartsWith('$'))
        {
            var variable = token[1..].Trim();
            if (!TryNormalizeVariableName(variable, out tokenValue))
            {
                return false;
            }

            operandType = ScriptOperandType.VariableReference;
            return true;
        }

        if (preferColor && ScreenPixelColor.TryParse(token, out var color))
        {
            operandType = ScriptOperandType.Color;
            tokenValue = color.ToString();
            return true;
        }

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            operandType = ScriptOperandType.Number;
            tokenValue = number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (bool.TryParse(token, out var boolValue))
        {
            operandType = ScriptOperandType.Boolean;
            tokenValue = boolValue ? "true" : "false";
            return true;
        }

        if (ScreenPixelColor.TryParse(token, out color))
        {
            operandType = ScriptOperandType.Color;
            tokenValue = color.ToString();
            return true;
        }

        operandType = ScriptOperandType.Text;
        tokenValue = EditorActionScriptTokens.UnescapeLiteralDollar(token);
        return true;
    }

    internal static bool TryParseConditionOperandToken(string rawToken, bool allowArithmetic, bool preferColor, out ScriptOperandType operandType, out string tokenValue)
    {
        // Numeric comparisons restore arithmetic operands structured (canonical Format(), type mirrors left operand); equality/text/bool/color keep the plain path.
        if (allowArithmetic && ScriptNumericExpression.TryParse(rawToken, out var expression) && expression is { Op: not null })
        {
            operandType = expression.LeftSource is ScriptNumericSourceType.VariableReference ? ScriptOperandType.VariableReference : ScriptOperandType.Number;
            tokenValue = ScriptNumericExpression.Format(expression);
            return true;
        }

        return TryParseOperandToken(rawToken, out operandType, out tokenValue, preferColor);
    }

    internal static bool TryInferSetValue(string rawValue, out ScriptValueType valueType, out string value)
    {
        valueType = ScriptValueType.Text;
        value = string.Empty;
        var token = rawValue.Trim();
        if (token.Length is 0)
        {
            return false;
        }

        if (token.StartsWith("$$", StringComparison.Ordinal))
        {
            valueType = ScriptValueType.Text;
            value = EditorActionScriptTokens.UnescapeLiteralDollar(token);
            return true;
        }

        if (token.StartsWith('$'))
        {
            var variable = token[1..].Trim();
            if (!TryNormalizeVariableName(variable, out value))
            {
                return false;
            }

            valueType = ScriptValueType.VariableReference;
            return true;
        }

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            valueType = ScriptValueType.Number;
            value = number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (bool.TryParse(token, out var boolValue))
        {
            valueType = ScriptValueType.Boolean;
            value = boolValue ? "true" : "false";
            return true;
        }

        valueType = ScriptValueType.Text;
        value = EditorActionScriptTokens.UnescapeLiteralDollar(token);
        return true;
    }

    internal static bool TryNormalizeVariableName(string rawValue, out string variableName)
    {
        variableName = EditorActionScriptTokens.NormalizeVariableToken(rawValue);
        return EditorActionScriptTokens.IsValidVariableName(variableName);
    }
}
