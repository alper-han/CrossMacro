using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Executes "window ..." script steps at runtime using the platform's IWindowManager.
/// </summary>
internal sealed class RunScriptWindowExecutor
{
    private readonly IWindowQueryService _queryService;
    private readonly IWindowMutationService _mutationService;
    private readonly IWorkspaceManagementService _workspaceService;

    internal const string CommandToken = "window";

    private static readonly Dictionary<string, IWindowCommandHandler> _handlers;

    static RunScriptWindowExecutor()
    {
        var handlers = new IWindowCommandHandler[]
        {
            new WindowActiveCommandHandler(),
            new WindowSearchCommandHandler(),
            new WindowFocusCommandHandler(),
            new WindowCloseCommandHandler(),
            new WindowWaitCommandHandler(),
            new WindowMoveCommandHandler(),
            new WindowResizeCommandHandler(),
            new WindowStateCommandHandler("fullscreen"),
            new WindowStateCommandHandler("float"),
            new WindowStateCommandHandler("center"),
            new WindowWorkspaceCommandHandler("getdesktop"),
            new WindowWorkspaceCommandHandler("setdesktop"),
            new WindowWorkspaceCommandHandler("setdesktopforwindow")
        };
        _handlers = handlers.ToDictionary(h => h.SubCommand, StringComparer.OrdinalIgnoreCase);
    }

    public RunScriptWindowExecutor(IWindowManager windowManager)
    {
        ArgumentNullException.ThrowIfNull(windowManager);
        _queryService = windowManager;
        _mutationService = windowManager;
        _workspaceService = windowManager;
    }

    /// <summary>Returns true if the step starts with the "window" command token.</summary>
    public static bool IsWindowStep(string step) =>
        step.StartsWith(CommandToken + " ", StringComparison.OrdinalIgnoreCase)
        || step.Equals(CommandToken, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Validates a window step at compile time. Returns an error string on failure, null on success.
    /// </summary>
    public static string? Validate(string step)
    {
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !parts[0].Equals(CommandToken, StringComparison.OrdinalIgnoreCase))
            return $"Invalid window syntax: '{step}'.";

        var sub = parts[1].ToLowerInvariant();
        if (!_handlers.TryGetValue(sub, out var handler))
            return $"Unknown window sub-command '{sub}'. Expected: {string.Join(", ", _handlers.Keys)}.";

        return handler.Validate(parts);
    }

    /// <summary>
    /// Executes a window step at runtime, resolving variables from <paramref name="variables"/>.
    /// </summary>
    public async Task ExecuteStepAsync(
        string step,
        int stepNumber,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var resolvedStep = ResolveVariables(step, variables, stepNumber);

        var parts = resolvedStep.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !parts[0].Equals(CommandToken, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Step {stepNumber}: Invalid window syntax: '{resolvedStep}'.");

        var sub = parts[1].ToLowerInvariant();
        if (!_handlers.TryGetValue(sub, out var handler))
            throw new InvalidOperationException($"Step {stepNumber}: Unknown window sub-command '{sub}'.");

        var error = handler.Validate(parts);
        if (error != null)
            throw new InvalidOperationException($"Step {stepNumber}: {error}");

        await handler.ExecuteAsync(parts, variables, stepNumber, _queryService, _mutationService, _workspaceService, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveVariables(string input, IDictionary<string, string> variables, int stepNumber)
    {
        if (!input.Contains('$'))
            return input;

        var sb = new System.Text.StringBuilder(input.Length);
        var i = 0;
        while (i < input.Length)
        {
            if (input[i] != '$')
            {
                sb.Append(input[i++]);
                continue;
            }

            if (i + 1 < input.Length && input[i + 1] == '$')
            {
                sb.Append('$');
                i += 2;
                continue;
            }

            var j = i + 1;
            while (j < input.Length && (char.IsLetterOrDigit(input[j]) || input[j] == '_'))
                j++;

            var varName = input[(i + 1)..j];
            if (!variables.TryGetValue(varName, out var value))
                throw new InvalidOperationException($"Step {stepNumber}: unknown variable '${varName}'.");

            sb.Append(value);
            i = j;
        }

        return sb.ToString();
    }
}
