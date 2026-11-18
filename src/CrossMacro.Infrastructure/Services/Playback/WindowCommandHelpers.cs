
namespace CrossMacro.Infrastructure.Services.Playback;

internal static class WindowCommandHelpers
{
    /// <summary>Timeout applied to <c>window wait</c> when the step does not give one.</summary>
    public const int DefaultWaitTimeoutMs = 5000;

    /// <summary>Pause between two polls of <c>window wait</c>.</summary>
    public static readonly TimeSpan WaitPollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>Pause that lets the compositor apply a state change before geometry is changed.</summary>
    public static readonly TimeSpan GeometrySettleDelay = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Output variables may be written bare (<c>result</c>) or with the script <c>$</c> prefix (<c>$result</c>);
    /// both name the same variable.
    /// </summary>
    public static bool TryGetOutputVariableName(string token, out string name) =>
        RunScriptVariableCommands.TryNormalizeName(token, out name, out _);

    public static WindowInfo? FindByTitle(IReadOnlyList<WindowInfo> windows, string substr) =>
        FindFirst(windows, w => w.Title.Contains(substr, StringComparison.OrdinalIgnoreCase));

    public static WindowInfo? FindByClass(IReadOnlyList<WindowInfo> windows, string substr) =>
        FindFirst(windows, w => w.Class.Contains(substr, StringComparison.OrdinalIgnoreCase));

    private static WindowInfo? FindFirst(IReadOnlyList<WindowInfo> windows, Func<WindowInfo, bool> predicate)
    {
        return windows.FirstOrDefault(predicate);
    }

    public static void StoreVariable(IDictionary<string, string> variables, string token, string value, int stepNumber)
    {
        if (!TryGetOutputVariableName(token, out var name))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: invalid variable name '{token}'.");
        }

        variables[name] = value;
    }
}
