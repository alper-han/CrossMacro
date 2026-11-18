namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Classifies script value tokens into the typed payloads editor actions store.</summary>
internal static class EditorScriptTokenReader
{
    internal static bool TryNormalizeVariableName(string rawValue, out string variableName)
    {
        variableName = EditorActionScriptTokens.NormalizeVariableToken(rawValue);
        return ScriptIdentifierSyntax.IsValidVariableName(variableName);
    }

    /// <summary>
    /// Restores a binary arithmetic expression structured: the canonical <c>Format()</c> string, with the
    /// source type mirroring the left operand.
    /// </summary>
    internal static bool TryParseBinaryExpression(string raw, out ScriptNumericSourceType leftSource, out string formatted)
    {
        leftSource = ScriptNumericSourceType.Number;
        formatted = string.Empty;
        if (!ScriptNumericExpression.TryParse(raw, out var expression) || expression is not { Op: not null })
        {
            return false;
        }

        leftSource = expression.LeftSource;
        formatted = ScriptNumericExpression.Format(expression);
        return true;
    }

    /// <summary>Segments are 1 or 3 tokens; longer segments fall back to raw text.</summary>
    internal static bool TryParseNumericSegment(string[] tokens, out ScriptNumericSourceType sourceType, out string value)
    {
        sourceType = ScriptNumericSourceType.Number;
        value = string.Empty;
        return tokens.Length switch
        {
            1 => EditorActionScriptTokens.TryParseNumericToken(tokens[0], out sourceType, out value),
            3 => TryParseBinaryExpression(string.Join(' ', tokens), out sourceType, out value),
            _ => false,
        };
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

    /// <summary>
    /// Numeric comparisons restore arithmetic operands structured (type mirrors the left operand);
    /// equality/text/bool/color keep the plain path.
    /// </summary>
    internal static bool TryParseConditionOperandToken(string rawToken, bool allowArithmetic, bool preferColor, out ScriptOperandType operandType, out string tokenValue)
    {
        if (allowArithmetic && TryParseBinaryExpression(rawToken, out var leftSource, out tokenValue))
        {
            operandType = leftSource is ScriptNumericSourceType.VariableReference ? ScriptOperandType.VariableReference : ScriptOperandType.Number;
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
}
