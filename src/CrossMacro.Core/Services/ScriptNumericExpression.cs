
namespace CrossMacro.Core.Services;

/// <summary>
/// Parsed numeric expression: simple value (int literal or $variable, Op null) or binary
/// &lt;operand&gt; &lt;op&gt; &lt;operand&gt; over + - * / %. Evaluation resolves operands, then
/// computes in long with one range check (no throw, no wrap). The lexer is unary-minus aware
/// ("5*-3" parses; "5 * - 3" is malformed).
/// </summary>
public sealed record ScriptNumericExpression(
    ScriptNumericSourceType LeftSource,
    string LeftValue,
    ScriptArithmeticOperation? Op,
    ScriptNumericSourceType RightSource,
    string RightValue)
{
    // Default context keeps the legacy set-command wording for shared error templates.
    private const string DefaultErrorContext = "set expressions";

    public static bool TryParse(string token, out ScriptNumericExpression? expression)
    {
        return TryParseCore(token, out expression, out _);
    }

    public static string Format(ScriptNumericExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (expression.Op is null)
        {
            return expression.LeftValue;
        }

        var opToken = expression.Op.Value switch
        {
            ScriptArithmeticOperation.Add => "+",
            ScriptArithmeticOperation.Subtract => "-",
            ScriptArithmeticOperation.Multiply => "*",
            ScriptArithmeticOperation.Divide => "/",
            ScriptArithmeticOperation.Modulo => "%",
            _ => throw new ArgumentOutOfRangeException(
                nameof(expression),
                expression.Op,
                "Unsupported arithmetic operation."),
        };

        return string.Concat(expression.LeftValue, " ", opToken, " ", expression.RightValue);
    }

    public static bool Evaluate(
        ScriptNumericExpression expression,
        IReadOnlyDictionary<string, string> variables,
        out int value,
        out string? error)
    {
        return EvaluateCore(expression, variables, contextLabel: null, out value, out error);
    }

    /// <summary>
    /// Parses and evaluates in one step. NotExpression = plain text (raw fallback may apply);
    /// Malformed = committed but invalid. <paramref name="contextLabel"/> is inserted verbatim
    /// into error text (default: legacy "set expressions" wording).
    /// </summary>
    public static ScriptNumericEvaluationResult Evaluate(
        string token,
        IReadOnlyDictionary<string, string> variables,
        string? contextLabel = null)
    {
        ArgumentNullException.ThrowIfNull(variables);

        if (!TryParseCore(token, out var expression, out var malformed))
        {
            return malformed
                ? new ScriptNumericEvaluationResult(
                    Status: ScriptNumericExpressionStatus.Malformed,
                    Value: 0,
                    Error: FormatMalformedError(token, contextLabel))
                : new ScriptNumericEvaluationResult(
                    Status: ScriptNumericExpressionStatus.NotExpression,
                    Value: 0,
                    Error: null);
        }

        if (!EvaluateCore(expression!, variables, contextLabel, out var value, out var error))
        {
            return new ScriptNumericEvaluationResult(
                Status: ScriptNumericExpressionStatus.EvaluationError,
                Value: 0,
                Error: error);
        }

        return new ScriptNumericEvaluationResult(
            Status: ScriptNumericExpressionStatus.Evaluated,
            Value: value,
            Error: null);
    }

    private static bool EvaluateCore(
        ScriptNumericExpression expression,
        IReadOnlyDictionary<string, string> variables,
        string? contextLabel,
        out int value,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(variables);

        value = 0;

        if (!TryResolveOperand(expression.LeftSource, expression.LeftValue, variables, out var left, out error))
        {
            return false;
        }

        if (expression.Op is null)
        {
            value = left;
            return true;
        }

        if (!TryResolveOperand(expression.RightSource, expression.RightValue, variables, out var right, out error))
        {
            return false;
        }

        long result;
        switch (expression.Op.Value)
        {
            case ScriptArithmeticOperation.Add:
                result = (long)left + right;
                break;
            case ScriptArithmeticOperation.Subtract:
                result = (long)left - right;
                break;
            case ScriptArithmeticOperation.Multiply:
                result = (long)left * right;
                break;
            case ScriptArithmeticOperation.Divide:
                if (right is 0)
                {
                    error = $"Division by zero is not allowed in {ErrorContext(contextLabel)}.";
                    return false;
                }

                result = (long)left / right;
                break;
            case ScriptArithmeticOperation.Modulo:
                if (right is 0)
                {
                    error = $"Modulo by zero is not allowed in {ErrorContext(contextLabel)}.";
                    return false;
                }

                result = (long)left % right;
                break;
            default:
                error = $"Unsupported arithmetic operation '{expression.Op.Value}'.";
                return false;
        }

        if (result is < int.MinValue or > int.MaxValue)
        {
            error = $"Result is out of range for {ErrorContext(contextLabel)}.";
            return false;
        }

        value = (int)result;
        return true;
    }

    private static string FormatMalformedError(string token, string? contextLabel)
    {
        var baseMessage = $"'{token.Trim()}' is not a valid numeric expression.";
        return string.IsNullOrWhiteSpace(contextLabel)
            ? baseMessage
            : $"{baseMessage[..^1]} for {contextLabel}.";
    }

    private static string ErrorContext(string? contextLabel)
    {
        return string.IsNullOrWhiteSpace(contextLabel) ? DefaultErrorContext : contextLabel;
    }

    private static bool TryParseCore(string token, out ScriptNumericExpression? expression, out bool malformed)
    {
        expression = null;
        malformed = false;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        // Invalid piece = never an expression attempt; the set raw-string fallback applies.
        if (!TryLex(token.Trim(), out var pieces))
        {
            return false;
        }

        var hasOperator = false;
        foreach (var piece in pieces)
        {
            hasOperator |= piece.Kind is PieceKind.Operator;
        }

        if (!hasOperator)
        {
            if (pieces.Count is 1 && TryParseOperand(pieces[0].Text, out var singleSource))
            {
                expression = new ScriptNumericExpression(
                    singleSource,
                    pieces[0].Text,
                    Op: null,
                    ScriptNumericSourceType.Number,
                    string.Empty);
                return true;
            }

            return false;
        }

        if (pieces.Count is 3
            && pieces[0].Kind is not PieceKind.Operator
            && pieces[1].Kind is PieceKind.Operator
            && pieces[2].Kind is not PieceKind.Operator
            && TryParseOperand(pieces[0].Text, out var leftSource)
            && TryParseOperand(pieces[2].Text, out var rightSource))
        {
            // The lexer only emits operator pieces for the five known operator characters.
            var operation = ToOperation(pieces[1].Text[0])!.Value;
            expression = new ScriptNumericExpression(leftSource, pieces[0].Text, operation, rightSource, pieces[2].Text);
            return true;
        }

        malformed = true;
        return false;
    }

    private static bool TryLex(string expression, out List<Piece> pieces)
    {
        pieces = new List<Piece>();
        PieceKind? previousKind = null;
        var i = 0;
        while (i < expression.Length)
        {
            var ch = expression[i];
            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }

            if ((ch is '+' or '-') && previousKind is null or PieceKind.Operator
                && i + 1 < expression.Length && IsAsciiDigit(expression[i + 1]))
            {
                pieces.Add(new Piece(PieceKind.Number, LexNumber(expression, ref i, includeSign: true)));
            }
            else if (IsAsciiDigit(ch))
            {
                pieces.Add(new Piece(PieceKind.Number, LexNumber(expression, ref i, includeSign: false)));
            }
            else if (ch is '$')
            {
                var variable = LexVariable(expression, ref i);
                if (!IsVariableReference(variable))
                {
                    return false;
                }

                pieces.Add(new Piece(PieceKind.Variable, variable));
            }
            else if (ch is '+' or '-' or '*' or '/' or '%')
            {
                pieces.Add(new Piece(PieceKind.Operator, ch.ToString()));
                i++;
            }
            else
            {
                return false;
            }

            previousKind = pieces[pieces.Count - 1].Kind;
        }

        return true;
    }

    private static string LexNumber(string expression, ref int i, bool includeSign)
    {
        var start = i;
        if (includeSign)
        {
            i++;
        }

        while (i < expression.Length && IsAsciiDigit(expression[i]))
        {
            i++;
        }

        return expression[start..i];
    }

    private static string LexVariable(string expression, ref int i)
    {
        var start = i;
        i++;
        while (i < expression.Length && EditorActionScriptTokens.IsVariableNamePart(expression[i]))
        {
            i++;
        }

        return expression[start..i];
    }

    private static bool IsAsciiDigit(char ch)
    {
        return ch is >= '0' and <= '9';
    }

    /// <summary>Composes <see cref="TryParse"/> + <see cref="Evaluate"/>; false with null error means not an expression.</summary>
    public static bool TryEvaluate(
        string token,
        IReadOnlyDictionary<string, string> variables,
        out int value,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(variables);

        value = 0;
        error = null;

        if (!TryParse(token, out var expression) || expression is null)
        {
            return false;
        }

        return Evaluate(expression, variables, out value, out error);
    }

    private static bool TryParseOperand(string operandToken, out ScriptNumericSourceType source)
    {
        if (int.TryParse(operandToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            source = ScriptNumericSourceType.Number;
            return true;
        }

        if (IsVariableReference(operandToken))
        {
            source = ScriptNumericSourceType.VariableReference;
            return true;
        }

        source = ScriptNumericSourceType.Number;
        return false;
    }

    private static bool IsVariableReference(string token)
    {
        return token.StartsWith('$')
            && EditorActionScriptTokens.IsValidVariableName(token);
    }

    private static ScriptArithmeticOperation? ToOperation(char op)
    {
        return op switch
        {
            '+' => ScriptArithmeticOperation.Add,
            '-' => ScriptArithmeticOperation.Subtract,
            '*' => ScriptArithmeticOperation.Multiply,
            '/' => ScriptArithmeticOperation.Divide,
            '%' => ScriptArithmeticOperation.Modulo,
            _ => null,
        };
    }

    private static bool TryResolveOperand(
        ScriptNumericSourceType source,
        string operandValue,
        IReadOnlyDictionary<string, string> variables,
        out int value,
        out string? error)
    {
        value = 0;
        error = null;

        if (source is ScriptNumericSourceType.VariableReference)
        {
            var variableName = operandValue.StartsWith('$') ? operandValue[1..] : operandValue;
            if (!variables.TryGetValue(variableName, out var rawValue))
            {
                error = $"Unknown variable '${variableName}'.";
                return false;
            }

            if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = $"Variable '${variableName}' value '{rawValue}' is not a valid integer.";
                return false;
            }

            return true;
        }

        if (!int.TryParse(operandValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = $"Operand '{operandValue}' is not a valid integer.";
            return false;
        }

        return true;
    }

    private enum PieceKind
    {
        Number = 0,
        Variable = 1,
        Operator = 2,
    }

    private readonly record struct Piece(PieceKind Kind, string Text);
}
