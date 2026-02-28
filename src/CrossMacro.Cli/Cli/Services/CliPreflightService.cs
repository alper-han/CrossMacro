using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class CliPreflightService : ICliPreflightService
{
    private readonly IDisplaySessionService _displaySessionService;
    private readonly IInputSimulator _inputSimulator;
    private readonly IInputCapture _inputCapture;
    private readonly System.Func<bool> _isLinux;
    private readonly System.Func<string, string?> _getEnvironmentVariable;

    public CliPreflightService(
        IDisplaySessionService displaySessionService,
        IInputSimulator inputSimulator,
        IInputCapture inputCapture,
        System.Func<bool>? isLinux = null,
        System.Func<string, string?>? getEnvironmentVariable = null)
    {
        _displaySessionService = displaySessionService;
        _inputSimulator = inputSimulator;
        _inputCapture = inputCapture;
        _isLinux = isLinux ?? OperatingSystem.IsLinux;
        _getEnvironmentVariable = getEnvironmentVariable ?? System.Environment.GetEnvironmentVariable;
    }

    public Task<CliPreflightResult> CheckAsync(CliPreflightTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_displaySessionService.IsSessionSupported(out var sessionReason))
        {
            var errors = new List<string>();
            if (!string.IsNullOrWhiteSpace(sessionReason))
            {
                errors.Add(sessionReason);
            }

            return Task.FromResult(CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed: display session is not supported.",
                errors));
        }

        if (_isLinux())
        {
            var display = _getEnvironmentVariable("DISPLAY");
            var waylandDisplay = _getEnvironmentVariable("WAYLAND_DISPLAY");
            var hasDisplayVariable =
                !string.IsNullOrWhiteSpace(display) ||
                !string.IsNullOrWhiteSpace(waylandDisplay);

            if (!hasDisplayVariable)
            {
                return Task.FromResult(CliPreflightResult.Fail(
                    CliExitCode.EnvironmentError,
                    "Preflight check failed: no active Linux display session was detected.",
                    [
                        "DISPLAY and WAYLAND_DISPLAY are empty.",
                        "Run command inside an interactive desktop session, or configure daemon/display access correctly."
                    ]));
            }
        }

        if (target == CliPreflightTarget.Play || target == CliPreflightTarget.Run)
        {
            if (!_inputSimulator.IsSupported)
            {
                return Task.FromResult(CliPreflightResult.Fail(
                    CliExitCode.EnvironmentError,
                    "Preflight check failed: input simulation backend is unavailable.",
                    [$"Input simulator provider is not supported: {_inputSimulator.ProviderName}"]));
            }
        }

        if (target == CliPreflightTarget.Record)
        {
            if (!_inputCapture.IsSupported)
            {
                return Task.FromResult(CliPreflightResult.Fail(
                    CliExitCode.EnvironmentError,
                    "Preflight check failed: input capture backend is unavailable.",
                    [$"Input capture provider is not supported: {_inputCapture.ProviderName}"]));
            }
        }

        return Task.FromResult(CliPreflightResult.Ok());
    }
}
