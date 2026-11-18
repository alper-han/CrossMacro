
namespace CrossMacro.Cli.Services.Runtime;

public sealed class CliPreflightService : ICliPreflightService
{
    private readonly IDisplaySessionService _displaySessionService;
    private readonly Func<IInputSimulator> _inputSimulatorFactory;
    private readonly Func<IInputCapture> _inputCaptureFactory;
    private readonly Func<bool> _isLinux;
    private readonly Func<string, string?> _getEnvironmentVariable;

    public CliPreflightService(
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

    internal CliPreflightService(
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

    public async Task<CliPreflightResult> CheckAsync(CliPreflightTarget target, CancellationToken cancellationToken)
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

            return CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed: display session is not supported.",
                errors);
        }

        if (target is CliPreflightTarget.Headless && _isLinux() && string.IsNullOrWhiteSpace(_getEnvironmentVariable("DISPLAY")) && string.IsNullOrWhiteSpace(_getEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return CliPreflightResult.Fail(
                CliExitCode.EnvironmentError,
                "Preflight check failed: no active Linux display session was detected.",
                [
                    "DISPLAY and WAYLAND_DISPLAY are empty.",
                    "Headless mode requires an interactive desktop session.",
                ]);
        }

        if (target is CliPreflightTarget.Play or CliPreflightTarget.Run)
        {
            using var inputSimulator = _inputSimulatorFactory();
            if (!inputSimulator.IsSupported)
            {
                    return CliPreflightResult.Fail(
                        CliExitCode.EnvironmentError,
                        "Preflight check failed: input simulation backend is unavailable.",
                        [$"Input simulator provider is not supported: {inputSimulator.ProviderName}"]);
            }
        }

        if (target is CliPreflightTarget.Record)
        {
            using var inputCapture = _inputCaptureFactory();
            if (!inputCapture.IsSupported)
            {
                    return CliPreflightResult.Fail(
                        CliExitCode.EnvironmentError,
                        "Preflight check failed: input capture backend is unavailable.",
                        [$"Input capture provider is not supported: {inputCapture.ProviderName}"]);
            }
        }

        return CliPreflightResult.Ok();
    }
}
