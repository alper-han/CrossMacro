
namespace CrossMacro.Commands.Runtime;

public sealed class PreflightService : IPreflightService
{
    private readonly IDisplaySessionService _displaySessionService;
    private readonly Func<IInputSimulator> _inputSimulatorFactory;
    private readonly Func<IInputCapture> _inputCaptureFactory;
    private readonly Func<bool> _isLinux;
    private readonly Func<string, string?> _getEnvironmentVariable;

    public PreflightService(
        IRuntimeContext runtimeContext,
        IDisplaySessionService displaySessionService,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory)
        : this(
            displaySessionService,
            inputSimulatorFactory,
            inputCaptureFactory,
            () => runtimeContext.IsLinux,
            Environment.GetEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
    }

    internal PreflightService(
        IDisplaySessionService displaySessionService,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory,
        Func<bool> isLinux,
        Func<string, string?> getEnvironmentVariable)
    {
        _displaySessionService = displaySessionService;
        _inputSimulatorFactory = inputSimulatorFactory;
        _inputCaptureFactory = inputCaptureFactory;
        _isLinux = isLinux;
        _getEnvironmentVariable = getEnvironmentVariable;
    }

    public async Task<OperationResult> CheckAsync(PreflightTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionSupport = await _displaySessionService.IsSessionSupportedAsync(cancellationToken).ConfigureAwait(false);
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

        if (target is PreflightTarget.Headless && _isLinux() && string.IsNullOrWhiteSpace(_getEnvironmentVariable("DISPLAY")) && string.IsNullOrWhiteSpace(_getEnvironmentVariable("WAYLAND_DISPLAY")))
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
            using var inputSimulator = _inputSimulatorFactory();
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
            using var inputCapture = _inputCaptureFactory();
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
