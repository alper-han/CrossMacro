namespace CrossMacro.UI.ViewModels.Editor;

internal static class EditorStateComparer
{
    internal static bool AreActionsEquivalent(EditorAction left, EditorAction right)
    {
        return left.Type == right.Type
            && left.X == right.X
            && left.Y == right.Y
            && string.Equals(left.CoordinateXToken, right.CoordinateXToken, StringComparison.Ordinal)
            && string.Equals(left.CoordinateYToken, right.CoordinateYToken, StringComparison.Ordinal)
            && left.IsAbsolute == right.IsAbsolute
            && left.CoordinateSpace == right.CoordinateSpace
            && left.Button == right.Button
            && left.KeyCode == right.KeyCode
            && left.DelayMicroseconds == right.DelayMicroseconds
            && left.UseRandomDelay == right.UseRandomDelay
            && left.RandomDelayMinMs == right.RandomDelayMinMs
            && left.RandomDelayMaxMs == right.RandomDelayMaxMs
            && left.UseCurrentPosition == right.UseCurrentPosition
            && left.ScrollAmount == right.ScrollAmount
            && string.Equals(left.KeyName, right.KeyName, StringComparison.Ordinal)
            && string.Equals(left.Text, right.Text, StringComparison.Ordinal)
            && string.Equals(left.ScriptVariableName, right.ScriptVariableName, StringComparison.Ordinal)
            && string.Equals(left.MousePositionXVariableName, right.MousePositionXVariableName, StringComparison.Ordinal)
            && string.Equals(left.MousePositionYVariableName, right.MousePositionYVariableName, StringComparison.Ordinal)
            && left.ScriptValueType == right.ScriptValueType
            && string.Equals(left.ScriptValue, right.ScriptValue, StringComparison.Ordinal)
            && left.ScriptNumericSourceType == right.ScriptNumericSourceType
            && string.Equals(left.ScriptNumericValue, right.ScriptNumericValue, StringComparison.Ordinal)
            && left.ScriptLeftOperandType == right.ScriptLeftOperandType
            && string.Equals(left.ScriptLeftOperand, right.ScriptLeftOperand, StringComparison.Ordinal)
            && left.ScriptConditionOperator == right.ScriptConditionOperator
            && left.ScriptRightOperandType == right.ScriptRightOperandType
            && string.Equals(left.ScriptRightOperand, right.ScriptRightOperand, StringComparison.Ordinal)
            && string.Equals(left.ForVariableName, right.ForVariableName, StringComparison.Ordinal)
            && left.ForStartType == right.ForStartType
            && string.Equals(left.ForStartValue, right.ForStartValue, StringComparison.Ordinal)
            && left.ForEndType == right.ForEndType
            && string.Equals(left.ForEndValue, right.ForEndValue, StringComparison.Ordinal)
            && left.ForHasStep == right.ForHasStep
            && left.ForStepType == right.ForStepType
            && string.Equals(left.ForStepValue, right.ForStepValue, StringComparison.Ordinal)
            && left.ScreenX == right.ScreenX
            && left.ScreenY == right.ScreenY
            && string.Equals(left.ScreenColorHex, right.ScreenColorHex, StringComparison.Ordinal)
            && left.ScreenTargetColorSource == right.ScreenTargetColorSource
            && string.Equals(left.ScreenTargetColorVariableName, right.ScreenTargetColorVariableName, StringComparison.Ordinal)
            && string.Equals(left.ScreenColorVariableName, right.ScreenColorVariableName, StringComparison.Ordinal)
            && left.ScreenTimeoutMs == right.ScreenTimeoutMs
            && left.ScreenTolerance == right.ScreenTolerance
            && left.ScreenLeft == right.ScreenLeft
            && left.ScreenTop == right.ScreenTop
            && left.ScreenWidth == right.ScreenWidth
            && left.ScreenHeight == right.ScreenHeight
            && string.Equals(left.ImageSearchRegionLeftToken, right.ImageSearchRegionLeftToken, StringComparison.Ordinal)
            && string.Equals(left.ImageSearchRegionTopToken, right.ImageSearchRegionTopToken, StringComparison.Ordinal)
            && string.Equals(left.ImageSearchRegionWidthToken, right.ImageSearchRegionWidthToken, StringComparison.Ordinal)
            && string.Equals(left.ImageSearchRegionHeightToken, right.ImageSearchRegionHeightToken, StringComparison.Ordinal)
            && string.Equals(left.ScreenFoundVariableName, right.ScreenFoundVariableName, StringComparison.Ordinal)
            && string.Equals(left.ScreenFoundXVariableName, right.ScreenFoundXVariableName, StringComparison.Ordinal)
            && string.Equals(left.ScreenFoundYVariableName, right.ScreenFoundYVariableName, StringComparison.Ordinal)
            && string.Equals(left.ImageAssetName, right.ImageAssetName, StringComparison.Ordinal)
            && left.ImageSearchSimilarity.CompareTo(right.ImageSearchSimilarity) is 0
            && left.ImageSearchMatchMode == right.ImageSearchMatchMode
            && left.ImageSearchMatchModeWasExplicit == right.ImageSearchMatchModeWasExplicit
            && left.ShellCommandMode == right.ShellCommandMode
            && string.Equals(left.ShellCommand, right.ShellCommand, StringComparison.Ordinal)
            && string.Equals(left.ShellStandardInput, right.ShellStandardInput, StringComparison.Ordinal)
            && string.Equals(left.ShellExitCodeVariableName, right.ShellExitCodeVariableName, StringComparison.Ordinal)
            && string.Equals(left.ShellStandardOutputVariableName, right.ShellStandardOutputVariableName, StringComparison.Ordinal)
            && string.Equals(left.ShellStandardErrorVariableName, right.ShellStandardErrorVariableName, StringComparison.Ordinal)
            && left.ShellRetries == right.ShellRetries
            && left.ShellBackoffMs == right.ShellBackoffMs
            && left.ShellTimeoutMs == right.ShellTimeoutMs
            && string.Equals(left.ScreenshotOutputPath, right.ScreenshotOutputPath, StringComparison.Ordinal)
            && left.ScreenshotCopyToClipboard == right.ScreenshotCopyToClipboard
            && left.ScreenshotUseRegion == right.ScreenshotUseRegion
            && string.Equals(left.ScreenshotRegionX, right.ScreenshotRegionX, StringComparison.Ordinal)
            && string.Equals(left.ScreenshotRegionY, right.ScreenshotRegionY, StringComparison.Ordinal)
            && string.Equals(left.ScreenshotRegionWidth, right.ScreenshotRegionWidth, StringComparison.Ordinal)
            && string.Equals(left.ScreenshotRegionHeight, right.ScreenshotRegionHeight, StringComparison.Ordinal)
            && left.WindowCommandMode == right.WindowCommandMode
            && string.Equals(left.WindowSelectorKind, right.WindowSelectorKind, StringComparison.Ordinal)
            && string.Equals(left.WindowSelectorValue, right.WindowSelectorValue, StringComparison.Ordinal)
            && string.Equals(left.WindowActiveField, right.WindowActiveField, StringComparison.Ordinal)
            && string.Equals(left.WindowOutputVariable, right.WindowOutputVariable, StringComparison.Ordinal)
            && left.WindowTimeoutMs == right.WindowTimeoutMs
            && left.WindowX == right.WindowX
            && left.WindowY == right.WindowY
            && left.WindowWidth == right.WindowWidth
            && left.WindowHeight == right.WindowHeight
            && string.Equals(left.WindowWorkspace, right.WindowWorkspace, StringComparison.Ordinal)
            && left.PreferLegacyScriptText == right.PreferLegacyScriptText;
    }

    internal static bool AreStatesEquivalent(EditorStateSnapshot left, EditorStateSnapshot right)
    {
        if (left.SkipInitialZeroZero != right.SkipInitialZeroZero
            || left.Actions.Count != right.Actions.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Actions.Count; index++)
        {
            if (!AreActionsEquivalent(left.Actions[index], right.Actions[index]))
            {
                return false;
            }
        }

        return true;
    }

}
