namespace CrossMacro.Infrastructure.Services.Editing;

internal static class EditorScriptWriter
{
    internal static List<RunScriptStep> BuildScriptSteps(IReadOnlyList<EditorAction> actions)
    {
        var steps = new List<RunScriptStep>();
        var sourceIndex = 0;
        foreach (var action in actions)
        {
            sourceIndex++;
            var actionSteps = ConvertActionToScriptSteps(action).Where(step => !string.IsNullOrWhiteSpace(step)).ToList();
            if (CanSkipLeadingAbsoluteMove(action, steps, actionSteps))
            {
                actionSteps.RemoveAt(0);
            }

            if (actionSteps.Count > 0 && action.Type is not EditorActionType.Delay)
            {
                string? delayStep = null;
                if (action.UseRandomDelay)
                {
                    delayStep = $"delay random {action.RandomDelayMinMs.ToString(CultureInfo.InvariantCulture)} {action.RandomDelayMaxMs.ToString(CultureInfo.InvariantCulture)}";
                }
                else if (action.DelayMicroseconds > 0)
                {
                    delayStep = $"delay {MacroTiming.FormatScriptDuration(action.DelayMicroseconds)}";
                }

                if (delayStep is not null)
                {
                    actionSteps.Insert(0, delayStep);
                }
            }

            foreach (var step in actionSteps)
            {
                steps.Add(new RunScriptStep(step, SourceLineNumber: null, sourceIndex));
            }
        }

        return steps;
    }

    internal static IEnumerable<string> ConvertActionToScriptSteps(EditorAction action) => action.Type switch
    {
        EditorActionType.MouseMove => [BuildMoveStep(action)],
        EditorActionType.MouseClick => BuildClickSteps(action),
        EditorActionType.MouseDown => BuildButtonSteps(action, "down"),
        EditorActionType.MouseUp => BuildButtonSteps(action, "up"),
        EditorActionType.KeyPress => [$"tap {action.KeyCode.ToString(CultureInfo.InvariantCulture)}"],
        EditorActionType.KeyDown => [$"key down {action.KeyCode.ToString(CultureInfo.InvariantCulture)}"],
        EditorActionType.KeyUp => [$"key up {action.KeyCode.ToString(CultureInfo.InvariantCulture)}"],
        EditorActionType.Delay => [BuildDelayStep(action)],
        EditorActionType.ScrollVertical => [BuildScrollStep(action, "up", "down")],
        EditorActionType.ScrollHorizontal => [BuildScrollStep(action, "right", "left")],
        EditorActionType.TextInput => [$"type {action.Text}"],
        EditorActionType.MousePosition => [$"{RunScriptSyntax.MouseCommand} {RunScriptSyntax.MousePositionCommand} {EditorActionScriptTokens.NormalizeVariableToken(action.MousePositionXVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.MousePositionYVariableName)}"],
        EditorActionType.SetVariable => [BuildSetStep(action)],
        EditorActionType.IncrementVariable => [BuildIncrementStep(action)],
        EditorActionType.DecrementVariable => [BuildDecrementStep(action)],
        EditorActionType.MultiplyVariable => [BuildMultiplyStep(action)],
        EditorActionType.DivideVariable => [BuildDivideStep(action)],
        EditorActionType.RepeatBlockStart => [BuildRepeatStep(action)],
        EditorActionType.IfBlockStart => [BuildConditionStep("if", action)],
        EditorActionType.ElseBlockStart => [RunScriptSyntax.ElseBlockHeader],
        EditorActionType.WhileBlockStart => [BuildConditionStep("while", action)],
        EditorActionType.ForBlockStart => [BuildForStep(action)],
        EditorActionType.PixelColor => [BuildPixelColorStep(action)],
        EditorActionType.WaitColor => [BuildWaitColorStep(action)],
        EditorActionType.PixelSearch => [BuildPixelSearchStep(action)],
        EditorActionType.ImageSearch => [BuildImageSearchStep(action)],
        EditorActionType.ImageClick => [BuildImageClickStep(action)],
        EditorActionType.WaitImage => [BuildWaitImageStep(action)],
        EditorActionType.ClipboardGet => [$"clipboard get {EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName)}"],
        EditorActionType.ClipboardSet => [$"clipboard set {action.Text}"],
        EditorActionType.CopySelectionToVariable => [$"clipboard capture {ClipboardCopyShortcutSyntax.ToScriptToken(action.ClipboardCopyShortcut)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName)}"],
        EditorActionType.ShellCommand => [BuildShellStep(action)],
        EditorActionType.Screenshot => [BuildScreenshotStep(action)],
        EditorActionType.WindowCommand => [BuildWindowStep(action)],
        EditorActionType.Break => [RunScriptSyntax.BreakCommand],
        EditorActionType.Continue => [RunScriptSyntax.ContinueCommand],
        EditorActionType.BlockEnd => [RunScriptSyntax.BlockEndToken],
        EditorActionType.RawScriptStep => [action.Text],
        _ => [],
    };

    private static string BuildMoveStep(EditorAction action) =>
        $"move {ToMouseMoveModeToken(action)} {action.CoordinateXToken} {action.CoordinateYToken}";

    private static string[] BuildClickSteps(EditorAction action) => action.UseCurrentPosition
        ? [$"click {RunScriptSyntax.CurrentPositionToken} {ToButtonToken(action.Button)}"]
        : [BuildMoveStep(action), $"click {ToButtonToken(action.Button)}"];

    private static string[] BuildButtonSteps(EditorAction action, string verb) => action.UseCurrentPosition
        ? [$"{verb} {RunScriptSyntax.CurrentPositionToken} {ToButtonToken(action.Button)}"]
        : [BuildMoveStep(action), $"{verb} {ToButtonToken(action.Button)}"];

    private static string BuildDelayStep(EditorAction action) => action.UseRandomDelay
        ? $"delay random {action.RandomDelayMinMs.ToString(CultureInfo.InvariantCulture)} {action.RandomDelayMaxMs.ToString(CultureInfo.InvariantCulture)}"
        : $"delay {MacroTiming.FormatScriptDuration(action.DelayMicroseconds)}";

    private static string BuildScrollStep(EditorAction action, string positiveDirection, string negativeDirection) =>
        $"scroll {(action.ScrollAmount > 0 ? positiveDirection : negativeDirection)} {Math.Abs(action.ScrollAmount).ToString(CultureInfo.InvariantCulture)}";

    internal static string ToMouseMoveModeToken(EditorAction action)
    {
        var coordinateMode = action.IsAbsolute ? MouseCoordinateMode.Absolute : MouseCoordinateMode.Relative;
        return RunScriptSyntax.ToMouseMoveModeToken(coordinateMode, action.CoordinateSpace);
    }

    internal static string BuildPixelColorStep(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);
        var variableName = payload.NormalizeColorVariableToken();
        return payload.IsAbsolute ? $"pixelcolor {payload.ScreenX.ToString(CultureInfo.InvariantCulture)} {payload.ScreenY.ToString(CultureInfo.InvariantCulture)} {variableName}" : $"pixelcolor rel {payload.ScreenX.ToString(CultureInfo.InvariantCulture)} {payload.ScreenY.ToString(CultureInfo.InvariantCulture)} {variableName}";
    }

    internal static string BuildWaitColorStep(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);
        var resultVariableName = payload.NormalizeColorVariableToken();
        return $"waitcolor {payload.ScreenX.ToString(CultureInfo.InvariantCulture)} {payload.ScreenY.ToString(CultureInfo.InvariantCulture)} {payload.FormatTargetColorToken()} {payload.ScreenTimeoutMs.ToString(CultureInfo.InvariantCulture)} {resultVariableName}";
    }

    internal static string BuildPixelSearchStep(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);
        var foundVariableName = payload.NormalizeFoundVariableToken();
        var xVariableName = payload.NormalizeFoundXVariableToken();
        var yVariableName = payload.NormalizeFoundYVariableToken();
        return $"pixelsearch {payload.ScreenLeft.ToString(CultureInfo.InvariantCulture)} {payload.ScreenTop.ToString(CultureInfo.InvariantCulture)} {payload.ScreenRight.ToString(CultureInfo.InvariantCulture)} {payload.ScreenBottom.ToString(CultureInfo.InvariantCulture)} {payload.FormatTargetColorToken()} {foundVariableName} {xVariableName} {yVariableName} timeout {payload.ScreenTimeoutMs.ToString(CultureInfo.InvariantCulture)} tolerance {payload.ScreenTolerance.ToString(CultureInfo.InvariantCulture)}";
    }

    internal static string BuildImageSearchStep(EditorAction action)
    {
        return BuildImageActionPrefix(RunScriptSyntax.ImageSearchCommand, action) + $" {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundXVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundYVariableName)}" + BuildImageActionMatchOptions(action, includesTimeout: false);
    }

    internal static string BuildImageClickStep(EditorAction action)
    {
        return BuildImageActionPrefix(RunScriptSyntax.ImageClickCommand, action) + $" {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundXVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundYVariableName)}" + $" button {ToImageClickButtonToken(action.Button)}" + BuildImageActionMatchOptions(action, includesTimeout: true);
    }

    internal static string BuildWaitImageStep(EditorAction action)
    {
        return BuildImageActionPrefix(RunScriptSyntax.WaitImageCommand, action) + $" {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundXVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundYVariableName)}" + BuildImageActionMatchOptions(action, includesTimeout: true);
    }

    internal static string BuildImageActionPrefix(string command, EditorAction action)
    {
        var imageName = EditorActionScriptTokens.NormalizeVariableToken(action.ImageAssetName);
        if (!action.TryGetLiteralImageSearchRegion(out var left, out var top, out var width, out var height))
        {
            return $"{command} region {action.ImageSearchRegionLeftToken} {action.ImageSearchRegionTopToken} {action.ImageSearchRegionWidthToken} {action.ImageSearchRegionHeightToken} {imageName}";
        }

        var right = checked(left + width);
        var bottom = checked(top + height);
        return $"{command} {left.ToString(CultureInfo.InvariantCulture)} {top.ToString(CultureInfo.InvariantCulture)} {right.ToString(CultureInfo.InvariantCulture)} {bottom.ToString(CultureInfo.InvariantCulture)} {imageName}";
    }

    internal static string BuildImageActionMatchOptions(EditorAction action, bool includesTimeout)
    {
        var similarity = action.ImageSearchSimilarity.ToString("0.################", CultureInfo.InvariantCulture);
        var mode = action.ImageSearchMatchModeWasExplicit ? $" matchmode {RunScriptPlatformSyntax.ToImageMatchModeToken(action.ImageSearchMatchMode)}" : string.Empty;
        var timeout = includesTimeout ? $" timeout {action.ScreenTimeoutMs.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        return $"{timeout} similarity {similarity}{mode}";
    }

    internal static string BuildShellStep(EditorAction action)
    {
        if (!action.TryGetShellPayload(out var payload))
        {
            throw new ArgumentException("Action type must be a shell command.", nameof(action));
        }

        var command = QuoteShellField(payload.Command);
        var options = BuildShellOptions(payload);
        return payload.Mode switch
        {
            ShellCommandMode.ShellCapture => $"shell capture {command} {FormatShellCaptureTarget(payload.ExitCodeVariableName)} {FormatShellCaptureTarget(payload.StandardOutputVariableName)} {FormatShellCaptureTarget(payload.StandardErrorVariableName)}{options}",
            ShellCommandMode.ShellInput => $"shell input {QuoteShellField(payload.StandardInput)} {command}{options}",
            ShellCommandMode.ShellCaptureInput => $"shell capture-input {QuoteShellField(payload.StandardInput)} {command} {FormatShellCaptureTarget(payload.ExitCodeVariableName)} {FormatShellCaptureTarget(payload.StandardOutputVariableName)} {FormatShellCaptureTarget(payload.StandardErrorVariableName)}{options}",
            ShellCommandMode.Shell => $"shell {command}{options}",
            _ => $"shell {command}{options}",
        };
    }

    internal static string BuildScreenshotStep(EditorAction action)
    {
        if (!action.TryGetScreenshotStep(out var step))
        {
            throw new ArgumentException("Action type must be a screenshot.", nameof(action));
        }

        var parts = new List<string>
        {
            RunScriptSyntax.ScreenshotCommand,
        };
        if (step.UseRegion)
        {
            parts.AddRange(["region", step.RegionX, step.RegionY, step.RegionWidth, step.RegionHeight]);
        }

        if (!string.IsNullOrWhiteSpace(step.OutputPath))
        {
            parts.Add("output");
            parts.Add(QuoteScreenshotOutputPath(step.OutputPath));
        }

        if (step.CopyToClipboard)
        {
            parts.Add("clipboard");
        }

        return string.Join(' ', parts);
    }

    internal static string BuildWindowStep(EditorAction action)
    {
        if (!action.TryGetWindowPayload(out var payload))
        {
            throw new ArgumentException("Action type must be a window command.", nameof(action));
        }

        var selectorKind = string.IsNullOrWhiteSpace(payload.SelectorKind) ? "title" : EditorWindowScriptReader.NormalizeSelectorKind(payload.SelectorKind);
        var selectorValue = QuoteWindowField(payload.SelectorValue);
        var outputVariable = EditorActionScriptTokens.NormalizeVariableToken(payload.OutputVariable);
        var workspace = QuoteWindowField(payload.Workspace);
        return payload.Mode switch
        {
            WindowCommandMode.Active => $"window active {payload.ActiveField} {outputVariable}",
            WindowCommandMode.Search => $"window search {selectorKind} {selectorValue} {outputVariable}",
            WindowCommandMode.Wait => $"window wait {selectorKind} {selectorValue} {payload.TimeoutMs.ToString(CultureInfo.InvariantCulture)} {outputVariable}",
            WindowCommandMode.Focus when selectorKind is "active" => "window focus active",
            WindowCommandMode.Focus => $"window focus {selectorKind} {selectorValue}",
            WindowCommandMode.Close when selectorKind is "active" => "window close active",
            WindowCommandMode.Close => $"window close {selectorKind} {selectorValue}",
            WindowCommandMode.Move => $"window move {payload.X.ToString(CultureInfo.InvariantCulture)} {payload.Y.ToString(CultureInfo.InvariantCulture)}",
            WindowCommandMode.Resize => $"window resize {payload.Width.ToString(CultureInfo.InvariantCulture)} {payload.Height.ToString(CultureInfo.InvariantCulture)}",
            WindowCommandMode.Center => "window center active",
            WindowCommandMode.Maximize => "window maximize active",
            WindowCommandMode.Fullscreen => "window fullscreen active",
            WindowCommandMode.Floating => "window float active",
            WindowCommandMode.WorkspaceGet => $"window getdesktop {outputVariable}",
            WindowCommandMode.WorkspaceSwitch => $"window setdesktop {workspace}",
            WindowCommandMode.WorkspaceMoveActive => $"window setdesktopforwindow active {workspace}",
            WindowCommandMode.WorkspaceMoveWindow => $"window setdesktopforwindow address {payload.SelectorValue.Trim()} {workspace}",
            _ => "window active title $windowResult",
        };
    }

    internal static string QuoteWindowField(string value)
    {
        return $"\"{(value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    internal static string QuoteScreenshotOutputPath(string value)
    {
        return value.Any(char.IsWhiteSpace) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\\', StringComparison.Ordinal) ? $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;
    }

    internal static string BuildShellOptions(EditorActionShellPayload payload)
    {
        if (payload.TimeoutMs > 0)
        {
            return $" {payload.Retries.ToString(CultureInfo.InvariantCulture)} {payload.BackoffMs.ToString(CultureInfo.InvariantCulture)} {payload.TimeoutMs.ToString(CultureInfo.InvariantCulture)}";
        }

        if (payload.BackoffMs > 0)
        {
            return $" {payload.Retries.ToString(CultureInfo.InvariantCulture)} {payload.BackoffMs.ToString(CultureInfo.InvariantCulture)}";
        }

        return payload.Retries > 0 ? $" {payload.Retries.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
    }

    internal static string FormatShellCaptureTarget(string target)
    {
        return target is "_" ? "_" : EditorActionScriptTokens.NormalizeVariableToken(target);
    }

    internal static string QuoteShellField(string value)
    {
        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    internal static EditorActionScreenReadingPayload GetScreenReadingPayload(EditorAction action)
    {
        if (!action.TryGetScreenReadingPayload(out var payload))
        {
            throw new InvalidOperationException("Action type does not contain a screen-reading payload.");
        }

        return payload;
    }

    internal static string ToButtonToken(MacroMouseButton button)
    {
        return button switch
        {
            MacroMouseButton.Left => "left",
            MacroMouseButton.Right => "right",
            MacroMouseButton.Middle => "middle",
            MacroMouseButton.Side1 => "side1",
            MacroMouseButton.Side2 => "side2",
            MacroMouseButton.None => "left",
            MacroMouseButton.ScrollUp => "left",
            MacroMouseButton.ScrollDown => "left",
            MacroMouseButton.ScrollLeft => "left",
            MacroMouseButton.ScrollRight => "left",
            _ => "left",
        };
    }

    internal static string ToImageClickButtonToken(MacroMouseButton button)
    {
        return button switch
        {
            MacroMouseButton.Right => "right",
            MacroMouseButton.Middle => "middle",
            MacroMouseButton.Left => "left",
            MacroMouseButton.None => "left",
            MacroMouseButton.ScrollUp => "left",
            MacroMouseButton.ScrollDown => "left",
            MacroMouseButton.ScrollLeft => "left",
            MacroMouseButton.ScrollRight => "left",
            MacroMouseButton.Side1 => "left",
            MacroMouseButton.Side2 => "left",
            _ => "left",
        };
    }

    internal static string BuildSetStep(EditorAction action)
    {
        if (ShouldSerializeLegacyText(action))
        {
            return $"set {action.Text}";
        }

        var name = EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName);
        var value = EditorActionScriptTokens.FormatSetValueToken(action.ScriptValueType, action.ScriptValue);
        if (action.ScriptValueType is ScriptValueType.Text && value.Contains('=', StringComparison.Ordinal))
        {
            return $"set {name}={value}";
        }

        return $"set {name} {value}";
    }

    internal static string BuildIncrementStep(EditorAction action)
    {
        if (ShouldSerializeLegacyText(action))
        {
            return $"inc {action.Text}";
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName);
        var amountToken = BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
        return $"inc {variableName} {amountToken}";
    }

    internal static string BuildDecrementStep(EditorAction action)
    {
        if (ShouldSerializeLegacyText(action))
        {
            return $"dec {action.Text}";
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName);
        var amountToken = BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
        return $"dec {variableName} {amountToken}";
    }

    internal static string BuildMultiplyStep(EditorAction action)
    {
        if (ShouldSerializeLegacyText(action))
        {
            return $"mul {action.Text}";
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName);
        var amountToken = BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
        return $"mul {variableName} {amountToken}";
    }

    internal static string BuildDivideStep(EditorAction action)
    {
        if (ShouldSerializeLegacyText(action))
        {
            return $"div {action.Text}";
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName);
        var amountToken = BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
        return $"div {variableName} {amountToken}";
    }

    internal static string BuildRepeatStep(EditorAction action)
    {
        if (ShouldSerializeLegacyText(action))
        {
            return $"repeat {action.Text} {{";
        }

        var countToken = BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
        return $"repeat {countToken} {{";
    }

    internal static string BuildConditionStep(string keyword, EditorAction action)
    {
        if (ShouldSerializeLegacyText(action))
        {
            return $"{keyword} {action.Text} {{";
        }

        var left = BuildOperandToken(action.ScriptLeftOperandType, action.ScriptLeftOperand);
        var op = ScriptConditionOperatorSyntax.Format(action.ScriptConditionOperator);
        var right = BuildOperandToken(action.ScriptRightOperandType, action.ScriptRightOperand);
        return $"{keyword} {left} {op} {right} {{";
    }

    internal static string BuildForStep(EditorAction action)
    {
        if (ShouldSerializeLegacyText(action))
        {
            return $"for {action.Text} {{";
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(action.ForVariableName);
        var start = BuildNumericToken(action.ForStartType, action.ForStartValue);
        var end = BuildNumericToken(action.ForEndType, action.ForEndValue);
        if (!action.ForHasStep)
        {
            return $"for {variableName} from {start} to {end} {{";
        }

        var step = BuildNumericToken(action.ForStepType, action.ForStepValue);
        return $"for {variableName} from {start} to {end} step {step} {{";
    }

    internal static string BuildNumericToken(ScriptNumericSourceType sourceType, string value)
    {
        // Expression values are stored canonical; emit verbatim (sigil formatting would corrupt them).
        if (ScriptNumericExpression.TryParse(value, out var expression) && expression is { Op: not null })
        {
            return value.Trim();
        }

        return EditorActionScriptTokens.FormatNumericToken(sourceType, value, defaultValue: string.Empty);
    }

    internal static string BuildOperandToken(ScriptOperandType operandType, string value)
    {
        // Expression operands are stored canonical; emit verbatim (mirrors BuildNumericToken).
        if (operandType is ScriptOperandType.Number or ScriptOperandType.VariableReference && ScriptNumericExpression.TryParse(value, out var expression) && expression is { Op: not null })
        {
            return value.Trim();
        }

        return EditorActionScriptTokens.FormatOperandToken(operandType, value);
    }

    internal static bool ShouldSerializeLegacyText(EditorAction action)
    {
        return action.PreferLegacyScriptText && !string.IsNullOrWhiteSpace(action.Text);
    }

    internal static bool CanSkipLeadingAbsoluteMove(EditorAction action, List<RunScriptStep> existingSteps, List<string> actionSteps)
    {
        if (action.Type is not (EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp) || action.UseCurrentPosition || !action.IsAbsolute || existingSteps.Count is 0 || actionSteps.Count is 0)
        {
            return false;
        }

        if (!EditorInputScriptReader.TryParseMoveStep(existingSteps[^1].Step, out var previousMode, out _, out var previousX, out var previousY) || previousMode is not MouseCoordinateMode.Absolute)
        {
            return false;
        }

        if (!EditorInputScriptReader.TryParseMoveStep(actionSteps[0], out var currentMode, out _, out var currentX, out var currentY) || currentMode is not MouseCoordinateMode.Absolute)
        {
            return false;
        }

        return string.Equals(previousX, currentX, StringComparison.Ordinal) && string.Equals(previousY, currentY, StringComparison.Ordinal);
    }
}
