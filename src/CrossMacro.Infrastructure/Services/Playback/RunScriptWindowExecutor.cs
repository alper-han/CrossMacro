
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Executes "window ..." script steps at runtime using the platform's IWindowManager.
/// </summary>
internal sealed class RunScriptWindowExecutor
{
    private readonly IWindowManager _windowManager;

    private readonly Dictionary<string, IWindowCommandHandler> _handlers;

    private static readonly IReadOnlyDictionary<string, IWindowCommandHandler> ValidationHandlers = CreateHandlers(TimeProvider.System);

    private static Dictionary<string, IWindowCommandHandler> CreateHandlers(
        TimeProvider timeProvider,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null) => new IWindowCommandHandler[]
        {
            new WindowActiveCommandHandler(),
            new WindowSearchCommandHandler(),
            new WindowFocusCommandHandler(),
            new WindowCloseCommandHandler(),
            new WindowWaitCommandHandler(timeProvider, delayAsync),
            new WindowMoveCommandHandler(delayAsync),
            new WindowResizeCommandHandler(delayAsync),
            new WindowStateCommandHandler("fullscreen", delayAsync),
            new WindowStateCommandHandler("maximize", delayAsync),
            new WindowStateCommandHandler("float", delayAsync),
            new WindowStateCommandHandler("center", delayAsync),
            new WindowWorkspaceCommandHandler("getdesktop"),
            new WindowWorkspaceCommandHandler("setdesktop"),
            new WindowWorkspaceCommandHandler("setdesktopforwindow"),
        }
        .ToDictionary(h => h.SubCommand, StringComparer.OrdinalIgnoreCase);

    public RunScriptWindowExecutor(
        IWindowManager windowManager,
        TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        ArgumentNullException.ThrowIfNull(windowManager);
        _handlers = CreateHandlers(timeProvider ?? TimeProvider.System, delayAsync);
        _windowManager = windowManager;
    }

    /// <summary>
    /// Validates a window step at compile time. Returns an error string on failure, null on success.
    /// </summary>
    public static string? Validate(string step)
    {
        string[] parts;
        try
        {
            parts = RunScriptSyntax.SplitQuotedTokens(step).ToArray();
        }
        catch (FormatException ex)
        {
            return $"Invalid window syntax: '{step}'. {ex.Message}";
        }

        if (parts.Length < 2 || !parts[0].Equals(RunScriptSyntax.WindowCommand, StringComparison.OrdinalIgnoreCase))
        {
            return $"Invalid window syntax: '{step}'.";
        }

        var sub = parts[1];
        if (!ValidationHandlers.TryGetValue(sub, out var handler))
        {
            return $"Unknown window sub-command '{sub}'. Expected: {string.Join(", ", ValidationHandlers.Keys)}.";
        }

        return handler.Validate(parts);
    }

    /// <summary>
    /// Executes a window step at runtime, resolving variable references from <paramref name="variables"/> in
    /// every token except the one that names the output variable.
    /// </summary>
    public async Task ExecuteStepAsync(
        string step,
        int stepNumber,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var stepPrefix = $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: ";
        string[] parts;
        try
        {
            parts = RunScriptSyntax.SplitQuotedTokens(step).ToArray();
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{stepPrefix}Invalid window syntax: '{step}'. {ex.Message}", ex);
        }

        if (parts.Length < 2 || !parts[0].Equals(RunScriptSyntax.WindowCommand, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{stepPrefix}Invalid window syntax: '{step}'.");
        }

        var sub = parts[1];
        if (!_handlers.TryGetValue(sub, out var handler))
        {
            throw new InvalidOperationException($"{stepPrefix}Unknown window sub-command '{sub}'.");
        }

        var outputIndex = handler.OutputVariableIndex(parts);
        for (var i = 2; i < parts.Length; i++)
        {
            if (i != outputIndex)
            {
                parts[i] = RunScriptRuntimeText.ResolveVariables(parts[i], variables, stepPrefix);
            }
        }

        var error = handler.Validate(parts);
        if (error is not null)
        {
            throw new InvalidOperationException($"{stepPrefix}{error}");
        }

        await handler.ExecuteAsync(parts, variables, stepNumber, _windowManager, cancellationToken).ConfigureAwait(false);
    }

}
