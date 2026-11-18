namespace CrossMacro.Mcp.Services.Security;

internal static class McpAutomationPolicy
{
    public const int MaximumTimeoutSeconds = 3_600;
    public const int DefaultTimeoutSeconds = MaximumTimeoutSeconds;
    public const int MaximumRepeatDelayMs = 3_600_000;
    public const int MaximumStepCount = 100;
    public const int MaximumStepCharacters = 16_384;
    public const int MaximumStepPayloadCharacters = 262_144;

    // CLI zero means unlimited. The MCP bridge gives that existing CLI form the finite default; every other value
    // follows the same 1..MaximumTimeoutSeconds rule as the dedicated automation arguments.
    public static bool TryFromCliTimeout(int value, string argumentName, out int seconds, out McpToolOutcome error) =>
        TryGetSeconds(value is 0 ? null : value, argumentName, DefaultTimeoutSeconds, allowZero: false, out seconds, out error);

    public static bool TryGetSeconds(int? value, string argumentName, int defaultValue, bool allowZero, out int seconds, out McpToolOutcome error)
    {
        seconds = value ?? defaultValue;
        if (seconds is < 0 or > MaximumTimeoutSeconds || (!allowZero && seconds is 0))
        {
            error = McpToolOutcomeMapper.InvalidArguments($"Automation {argumentName} must be between {(allowZero ? 0 : 1).ToString(CultureInfo.InvariantCulture)} and {MaximumTimeoutSeconds}.");
            return false;
        }
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }
}
