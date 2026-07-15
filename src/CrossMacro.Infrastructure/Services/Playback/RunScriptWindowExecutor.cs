using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
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
            new WindowStateCommandHandler("maximize"),
            new WindowStateCommandHandler("float"),
            new WindowStateCommandHandler("center"),
            new WindowWorkspaceCommandHandler("getdesktop"),
            new WindowWorkspaceCommandHandler("setdesktop"),
            new WindowWorkspaceCommandHandler("setdesktopforwindow"),
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
        string[] parts;
        try
        {
            parts = RunScriptSyntax.SplitQuotedTokens(step).ToArray();
        }
        catch (FormatException ex)
        {
            return $"Invalid window syntax: '{step}'. {ex.Message}";
        }

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
        var resolvedStep = RunScriptRuntimeText.ResolveVariables(step, variables, $"Step {stepNumber}: ");

        string[] parts;
        try
        {
            parts = RunScriptSyntax.SplitQuotedTokens(resolvedStep).ToArray();
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Step {stepNumber}: Invalid window syntax: '{resolvedStep}'. {ex.Message}", ex);
        }

        if (parts.Length < 2 || !parts[0].Equals(CommandToken, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Step {stepNumber}: Invalid window syntax: '{resolvedStep}'.");

        var sub = parts[1].ToLowerInvariant();
        if (!_handlers.TryGetValue(sub, out var handler))
            throw new InvalidOperationException($"Step {stepNumber}: Unknown window sub-command '{sub}'.");

        var error = handler.Validate(parts);
        if (error is not null)
            throw new InvalidOperationException($"Step {stepNumber}: {error}");

        await handler.ExecuteAsync(parts, variables, stepNumber, _queryService, _mutationService, _workspaceService, cancellationToken).ConfigureAwait(false);
    }

}
