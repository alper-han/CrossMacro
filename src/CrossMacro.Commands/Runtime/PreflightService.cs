using CrossMacro.Platform.Abstractions.Input.Capture;
using CrossMacro.Platform.Abstractions.Input.Simulation;
using CrossMacro.Platform.Abstractions.Runtime;

namespace CrossMacro.Commands.Runtime;

public sealed class PreflightService(
    IRuntimeContext runtimeContext,
    IDisplayEnvironmentDiagnostic displayEnvironment,
    IDisplaySessionService displaySessionService,
    Func<IInputSimulator> inputSimulatorFactory,
    Func<IInputCapture> inputCaptureFactory) : IPreflightService
{
    public async Task<OperationResult> CheckAsync(PreflightTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionSupport = await displaySessionService.IsSessionSupportedAsync(cancellationToken).ConfigureAwait(false);
        if (!sessionSupport.Supported)
        {
            var errors = new List<string>();
            if (!string.IsNullOrWhiteSpace(sessionSupport.Reason))
            {
                errors.Add(sessionSupport.Reason);
            }

            return OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Preflight check failed: display session is not supported.",
                errors);
        }

        if (target is PreflightTarget.Headless && runtimeContext.IsLinux && string.IsNullOrWhiteSpace(displayEnvironment.Display) && string.IsNullOrWhiteSpace(displayEnvironment.WaylandDisplay))
        {
            return OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Preflight check failed: no active Linux display session was detected.",
                [
                    "DISPLAY and WAYLAND_DISPLAY are empty.",
                    "Headless mode requires an interactive desktop session.",
                ]);
        }

        if (target is PreflightTarget.Play or PreflightTarget.Run)
        {
            using var inputSimulator = inputSimulatorFactory();
            if (!inputSimulator.IsSupported)
            {
                return OperationResult.Fail(
                    ExecutionOutcomeCode.EnvironmentError,
                    "Preflight check failed: input simulation backend is unavailable.",
                    [$"Input simulator provider is not supported: {inputSimulator.ProviderName}"]);
            }
        }

        if (target is PreflightTarget.Record)
        {
            using var inputCapture = inputCaptureFactory();
            if (!inputCapture.IsSupported)
            {
                return OperationResult.Fail(
                    ExecutionOutcomeCode.EnvironmentError,
                    "Preflight check failed: input capture backend is unavailable.",
                    [$"Input capture provider is not supported: {inputCapture.ProviderName}"]);
            }
        }

        return OperationResult.Ok("Preflight checks passed.");
    }
}
