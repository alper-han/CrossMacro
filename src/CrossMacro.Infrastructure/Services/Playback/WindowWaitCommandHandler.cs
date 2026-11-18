
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowWaitCommandHandler(
    TimeProvider? timeProvider = null,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null) : IWindowCommandHandler
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync = delayAsync
        ?? (Func<TimeSpan, CancellationToken, Task>)((delay, cancellationToken) =>
            Task.Delay(delay, timeProvider ?? TimeProvider.System, cancellationToken));

    public string SubCommand => "wait";

    public int OutputVariableIndex(string[] parts) => parts.Length - 1;
    public string? Validate(string[] parts)
    {
        if (parts.Length < 4)
        {
            return "Syntax: window wait title|class \"<term>\" [timeout_ms] <var>";
        }

        var field = parts[2].ToUpperInvariant();
        if (field is not ("TITLE" or "CLASS"))
        {
            return $"Unknown field '{parts[2]}'. Expected: title, class.";
        }

        var varPart = parts[^1];
        if (!TryGetOutputVariableName(varPart, out _))
        {
            return $"Invalid variable name '{varPart}'.";
        }

        var hasTimeout = parts.Length > 4
            && int.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out var tVal)
            && tVal > 0;
        var termEndIndex = hasTimeout ? parts.Length - 2 : parts.Length - 1;
        var term = Unquote(string.Join(' ', parts[3..termEndIndex]));
        if (string.IsNullOrWhiteSpace(term))
        {
            return "Search term cannot be empty.";
        }

        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowManager manager, CancellationToken cancellationToken)
    {
        var field = parts[2].ToUpperInvariant();
        var varName = parts[^1];
        int tVal = 0;
        var hasTimeout = parts.Length > 4 && int.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out tVal) && tVal > 0;
        var termEndIndex = hasTimeout ? parts.Length - 2 : parts.Length - 1;
        var timeoutMs = hasTimeout ? tVal : DefaultWaitTimeoutMs;
        var term = Unquote(string.Join(' ', parts[3..termEndIndex]));
        var startedAt = _timeProvider.GetTimestamp();
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        WindowInfo? found = null;
        while (_timeProvider.GetElapsedTime(startedAt) < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windows = await manager.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
            found = field is "TITLE" ? FindByTitle(windows, term) : FindByClass(windows, term);
            if (found != null)
            {
                break;
            }

            await _delayAsync(WaitPollInterval, cancellationToken).ConfigureAwait(false);
        }
        StoreVariable(variables, varName, found?.Address ?? string.Empty, stepNumber);
    }
}
