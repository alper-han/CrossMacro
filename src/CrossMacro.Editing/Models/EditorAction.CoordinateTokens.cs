namespace CrossMacro.Editing.Models;

public partial class EditorAction
{
    private static bool IsVariableCoordinateToken(string token)
    {
        return EditorActionScriptTokens.TryParseNumericToken(token, out var sourceType, out _)
            && sourceType is ScriptNumericSourceType.VariableReference;
    }

    private static bool IsValidImageSearchRegionToken(string token, bool mustBePositive)
    {
        if (!EditorActionScriptTokens.TryParseNumericToken(token, out var sourceType, out var value))
        {
            return false;
        }

        return !mustBePositive
            || sourceType is ScriptNumericSourceType.VariableReference
            || int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) > 0;
    }

    private void SetCoordinateToken(
        string? value,
        ref string? tokenOverride,
        ref int coordinate,
        string tokenPropertyName,
        string coordinatePropertyName)
    {
        if (TryApplyCoordinateToken(value, ref tokenOverride, ref coordinate, out var coordinateChanged))
        {
            NotifyCoordinateTokenChanged(coordinateChanged, tokenPropertyName, coordinatePropertyName);
        }
    }

    private void SetImageSearchRegionToken(
        string? value,
        ref string? tokenOverride,
        ref int coordinate,
        string tokenPropertyName,
        string coordinatePropertyName)
    {
        if (!TryApplyCoordinateToken(value, ref tokenOverride, ref coordinate, out var coordinateChanged))
        {
            return;
        }

        MarkStructuredScriptEdited();
        NotifyCoordinateTokenChanged(coordinateChanged, tokenPropertyName, coordinatePropertyName);
    }

    private static bool TryApplyCoordinateToken(
        string? value,
        ref string? tokenOverride,
        ref int coordinate,
        out bool coordinateChanged)
    {
        var previousToken = tokenOverride ?? coordinate.ToString(CultureInfo.InvariantCulture);
        var token = value?.Trim() ?? string.Empty;
        coordinateChanged = false;

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var literal))
        {
            coordinateChanged = coordinate != literal;
            coordinate = literal;
            tokenOverride = null;
            token = literal.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            tokenOverride = token;
        }

        return coordinateChanged || !string.Equals(previousToken, token, StringComparison.Ordinal);
    }

    private void NotifyCoordinateTokenChanged(bool coordinateChanged, string tokenPropertyName, string coordinatePropertyName)
    {
        if (coordinateChanged)
        {
            OnPropertyChanged(coordinatePropertyName);
        }

        OnPropertyChanged(tokenPropertyName);
    }

    private void SetImageSearchRegionLiteral(
        int value,
        ref int coordinate,
        ref string? tokenOverride,
        string coordinatePropertyName,
        string tokenPropertyName)
    {
        if (coordinate == value && tokenOverride is null)
        {
            return;
        }

        var tokenChanged = tokenOverride is not null;
        coordinate = value;
        tokenOverride = null;
        MarkStructuredScriptEdited();
        OnPropertyChanged(coordinatePropertyName);
        if (tokenChanged)
        {
            OnPropertyChanged(tokenPropertyName);
        }
    }
}
